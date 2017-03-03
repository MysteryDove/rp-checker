﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using RPChecker.Util;
using System.Drawing;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using RPChecker.Properties;
using System.Windows.Forms;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using RPChecker.Util.FilterProcess;

namespace RPChecker.Forms
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            AddCommand();
        }

        public readonly List<KeyValuePair<string, string>> FilePathsPair = new List<KeyValuePair<string, string>>();
        private readonly List<ReSulT> _fullData = new List<ReSulT>();
        private readonly StringBuilder _errorMessageBuilder = new StringBuilder();
        private bool _beginErrorRecord;
        private int _threshold = 30;

        private IProcess _coreProcess = new VsPipePSNRProcess();

        #region SystemMenu
        private SystemMenu _systemMenu;

        private void AddCommand()
        {
            _systemMenu = new SystemMenu(this);
            _systemMenu.AddCommand("检查更新(&U)", Updater.CheckUpdate, true);
            _systemMenu.AddCommand("使用PSNR(VS)", () =>
            {
                if (!(_coreProcess is VsPipePSNRProcess))
                {
                    _coreProcess = new VsPipePSNRProcess();
                }
            }, true);
            _systemMenu.AddCommand("使用PSNR(FF)", () =>
            {
                if (!(_coreProcess is FFmpegPSNRProcess))
                {
                    _coreProcess = new FFmpegPSNRProcess();
                }
            }, false);
            _systemMenu.AddCommand("使用SSIM(FF)", () =>
            {
                if (!(_coreProcess is FFmpegSSIMProcess))
                {
                    _coreProcess = new FFmpegSSIMProcess();
                }
            }, false);

        }

        protected override void WndProc(ref Message msg)
        {
            base.WndProc(ref msg);

            // Let it know all messages so it can handle WM_SYSCOMMAND
            // (This method is inlined)
            _systemMenu.HandleMessage(ref msg);
        }
        #endregion

        private void Form1_Load(object sender, EventArgs e)
        {
            Text = $"[VCB-Studio] RP Checker v{Assembly.GetExecutingAssembly().GetName().Version}";

            Point saved = ToolKits.String2Point(RegistryStorage.Load(@"Software\RPChecker", "location"));
            if (saved != new Point(-32000, -32000)) Location = saved;
            RegistryStorage.RegistryAddCount(@"Software\RPChecker\Statistics", @"Count");

            cbFPS.SelectedIndex     = 0;
            cbVpyFile.SelectedIndex = 0;
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            foreach (var item in current.GetFiles().Where(item => item.Extension.ToLower() == ".vpy"))
            {
                cbVpyFile.Items.Add(item);
            }
            btnAnalyze.Enabled = false;
            
            Updater.CheckUpdateWeekly("RPChecker");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            RegistryStorage.Save(Location.ToString(), @"Software\RPChecker", "Location");
        }

        private bool _loadFormOpened;
        private void btnLoad_Click(object sender, EventArgs e)
        {
            if (_loadFormOpened) return;
            FrmLoadFiles flf = new FrmLoadFiles(this);
            flf.Load   += (o, args) => _loadFormOpened = true;
            flf.Closed += (o, args) =>  {
                btnAnalyze.Enabled = FilePathsPair.Count > 0;
                _loadFormOpened = false;
            };
            flf.Show();
        }

        private void btnLog_Click(object sender, EventArgs e)
        {
            if (_errorMessageBuilder.Length == 0) return;
            MessageBox.Show(_errorMessageBuilder.ToString(), @"Log");
        }

        private void btnAbort_Click(object sender, EventArgs e)
        {
            try
            {
                _coreProcess.Abort = true;
            }
            catch (Exception ex)
            {
                new Task(() => MessageBox.Show(ex.Message, "Terminate Process Failed")).Start();
            }
        }

        private readonly double[] _frameRate = { 24000/1001.0, 24, 25, 30000/1001.0, 50, 60000/1001.0 };

        private void ChangeClipDisplay(int index)
        {
            if (index < 0 || index > _fullData.Count) return;
            btnChart.Enabled = _fullData[index].Data.Count > 0;
            UpdataGridView(_fullData[index], _frameRate[cbFPS.SelectedIndex]);
        }

        private void cbFileList_SelectionChangeCommitted(object sender, EventArgs e)
        {
            ChangeClipDisplay(cbFileList.SelectedIndex);
        }

        private void cbFPS_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbFileList.SelectedIndex < 0) return;
            double frameRate = _frameRate[cbFPS.SelectedIndex];
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if(row.Tag == null) continue;
                TimeSpan temp = ToolKits.Second2Time(((KeyValuePair<int, double>)row.Tag).Key / frameRate);
                row.Cells[2].Value = temp.Time2String();
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            _threshold = Convert.ToInt32(numericUpDown1.Value);
            if (_fullData == null || _fullData.Count == 0) return;
            UpdataGridView(_fullData[cbFileList.SelectedIndex], _frameRate[cbFPS.SelectedIndex]);
        }

        private void cbFileList_MouseEnter(object sender, EventArgs e) => toolTip1.Show(cbFileList.SelectedItem?.ToString(), (IWin32Window)sender);

        private void cbFileList_MouseLeave(object sender, EventArgs e) => toolTip1.RemoveAll();

        private bool Enable
        {
            set
            {
                btnAnalyze.Enabled = value;
                btnLoad.Enabled = value;
                btnLog.Enabled = value;
                btnChart.Enabled = value;
                cbFileList.Enabled = value;
                cbFPS.Enabled = value;
                cbVpyFile.Enabled = value;
                numericUpDown1.Enabled = value;
                btnAbort.Enabled = !value;
            }
        }

        private void UpdataGridView(ReSulT info, double frameRate)
        {
            dataGridView1.Rows.Clear();
            foreach (var item in info.Data)
            {
                if ((item.Value > _threshold && dataGridView1.RowCount > 450) || dataGridView1.RowCount > 2048) break;

                DataGridViewRow newRow = new DataGridViewRow {Tag = item};
                TimeSpan temp = ToolKits.Second2Time(item.Key / frameRate);
                newRow.CreateCells(dataGridView1, item.Key, $"{item.Value:F4}", temp.Time2String());
                newRow.DefaultCellStyle.BackColor = item.Value < _threshold
                    ? Color.FromArgb(233, 76, 60) : Color.FromArgb(46, 205, 112);
                dataGridView1.Rows.Add(newRow);
                Application.DoEvents();
            }
            Debug.WriteLine($"DataGridView with {dataGridView1.Rows.Count} lines");
        }

        private bool _errorDialogShowed;
        private void btnAnalyze_Click(object sender, EventArgs e)
        {
            _fullData.Clear();
            _errorMessageBuilder.Clear();
            cbFileList.Items.Clear();
            foreach (var item in FilePathsPair)
            {
                try
                {
                    _errorDialogShowed = false;
                    AnalyseClip(item.Key, item.Value);
                    var data = _tempData.ToList().OrderBy(a => a.Value).ThenBy(b => b.Key).ToList();
                    _fullData.Add(new ReSulT {FileName = item.Value, Data = data});
                    if (_beginErrorRecord) continue; AddStatic();
                    if (_coreProcess is FFmpegSSIMProcess || _remainFile) continue; RemoveScript(item);
                }
                catch (Exception ex)
                {
                    new Task(() => MessageBox.Show(
                                $"{item.Key}{Environment.NewLine}{item.Value}{Environment.NewLine}{ex.Message}",
                                @"RPChecker ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error)).Start();
                }
            }
            _fullData.ForEach(item => cbFileList.Items.Add(Path.GetFileName(item.FileName) ?? ""));
            btnLog.Enabled = _errorMessageBuilder.ToString().Split('\n').Length > FilePathsPair.Count + 1;
            if (cbFileList.Items.Count <= 0) return;
            cbFileList.SelectedIndex = 0;
            ChangeClipDisplay(cbFileList.SelectedIndex);
        }

        private void AnalyseClip(string file1, string file2)
        {
            _coreProcess.ProgressUpdated += ProgressUpdated;
            _coreProcess.ValueUpdated += ValueUpdated;
            _tempData = new Dictionary<int, double>();
            
            _beginErrorRecord = false;
            Enable = false;
            toolStripStatusStdError.Text = _coreProcess.Loading;
            toolStripProgressBar1.Value = 0;
            try
            {
                var coreThread = new Thread(_coreProcess.GenerateLog);

                if (_coreProcess is VsPipePSNRProcess)
                {
                    string vsFile = $"{file2}.vpy";
                    ToolKits.GenerateVpyFile(file1, file2, vsFile, cbVpyFile.SelectedItem.ToString());
                    _errorMessageBuilder.AppendLine($"---{vsFile}---");
                    coreThread.Start(vsFile);
                }
                else
                {
                    _errorMessageBuilder.AppendLine($"---{Path.GetFileName(file1)}|{Path.GetFileName(file2)}---");
                    coreThread.Start(new KeyValuePair<string, string>(file1, file2));
                }

                while (coreThread.ThreadState != System.Threading.ThreadState.Stopped) Application.DoEvents();
                if (_coreProcess.Exceptions != null)
                {
                    toolStripStatusStdError.Text = _coreProcess.Exceptions.Message;
                    throw _coreProcess.Exceptions;
                }
            }
            catch (Exception ex)
            {
                new Task(() => MessageBox.Show(ex.Message, @"RPChecker Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)).Start();
            }
            finally
            {
                _coreProcess.ProgressUpdated -= ProgressUpdated;
                _coreProcess.ValueUpdated -= ValueUpdated;
                toolStripProgressBar1.Value = 100;
                Enable = true;
                Refresh();
                Application.DoEvents();
            }
        }

        private void ProgressUpdated(string progress)
        {
            if (string.IsNullOrEmpty(progress))return;
            _coreProcess
                .Match<VsPipePSNRProcess>(_ => Invoke(new Action(() => VsUpdateProgress(progress))))
                .Match<FFmpegProcess>(_ => Invoke(new Action(() => FFmpegUpdateProgress(progress))))
                ;
        }

        private void ValueUpdated(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            _coreProcess
                .Match<VsPipePSNRProcess>(_ => Invoke(new Action(() => UpdatePSNR(data))))
                .Match<FFmpegProcess>(_ => Invoke(new Action(() => _.UpdateValue(data, ref _tempData))))
                ;
        }


        private Dictionary<int, double> _tempData = new Dictionary<int, double>();

        #region vapoursynth
        private static readonly Regex VsProgressRegex = new Regex(@"Frame: (?<processed>\d+)/(?<total>\d+)", RegexOptions.Compiled);

        private void VsUpdateProgress(string progress)
        {
            toolStripStatusStdError.Text = progress;
            if (Regex.IsMatch(progress, "Failed", RegexOptions.IgnoreCase))
            {
                _beginErrorRecord = true;
            }
            if (_beginErrorRecord)
            {
                _errorMessageBuilder.AppendLine(progress);
                if (!_errorDialogShowed && progress.EndsWith("No module named 'mvsfunc'"))
                {
                    _errorDialogShowed = true;
                    new Task(() => MessageBox.Show(caption: @"RPChecker ERROR", icon: MessageBoxIcon.Error,
                        buttons: MessageBoxButtons.OK,
                        text: $"尚未正确放置mawen菊苣的滤镜库 'mvsfunc'{Environment.NewLine}大概的位置是在Python35\\Lib\\site-packages")).Start();
                }
                else if (!_errorDialogShowed && progress.EndsWith("There is no function named PlaneAverage"))
                {
                    _errorDialogShowed = true;
                    new Task(() => MessageBox.Show(caption: @"RPChecker ERROR", icon: MessageBoxIcon.Error,
                        buttons: MessageBoxButtons.OK,
                        text: $"请升级 'mvsfunc' 至少至 r6{Environment.NewLine}大概的位置是在Python35\\Lib\\site-packages")).Start();
                }
                return;
            }

            var ret = VsProgressRegex.Match(progress);
            if (!ret.Success) return;
            var processed = int.Parse(ret.Groups["processed"].Value);
            var total = int.Parse(ret.Groups["total"].Value);
            if (processed <= total)
            {
                toolStripProgressBar1.Value = (int)Math.Floor(processed * 100.0 / total);
            }
            Application.DoEvents();
        }

        private static readonly Regex PSNRDataFormatRegex = new Regex(@"(?<frame>\d+) (?<PSNR>[-+]?[0-9]*\.?[0-9]+)", RegexOptions.Compiled);

        private void UpdatePSNR(string data)
        {
            var rawData = PSNRDataFormatRegex.Match(data);
            if (!rawData.Success) return;
            _tempData[int.Parse(rawData.Groups["frame"].Value)] = double.Parse(rawData.Groups["PSNR"].Value);
        }
        #endregion

        #region ffmpeg

        private int _ffmpegTotalFrame = int.MaxValue;
        private static readonly Regex FFmpegFrameRegex = new Regex(@"NUMBER_OF_FRAMES: (?<frame>\d+)", RegexOptions.Compiled);
        private static readonly Regex FFmpegProgressRegex = new Regex(@"frame=\s*(?<processed>\d+)", RegexOptions.Compiled);
        private void FFmpegUpdateProgress(string progress)
        {
            // NUMBER_OF_FRAMES: 960
            //frame=  287 fps= 57 q=-0.0 size=N/A time=00:00:04.78 bitrate=N/A speed=0.953x
            toolStripStatusStdError.Text = progress;
            if (progress.StartsWith("[Parsed_ssim"))
            {
                _errorMessageBuilder.AppendLine(progress);
                return;
            }
            var frameRet = FFmpegFrameRegex.Match(progress);
            if (_ffmpegTotalFrame == int.MaxValue && frameRet.Success)
            {
                _ffmpegTotalFrame = int.Parse(frameRet.Groups["frame"].Value);
                return;
            }
            var ret = FFmpegProgressRegex.Match(progress);
            if (!ret.Success) return;
            var processed = int.Parse(ret.Groups["processed"].Value);
            if (processed <= _ffmpegTotalFrame)
            {
                toolStripProgressBar1.Value = (int)Math.Floor(processed * 100.0 / _ffmpegTotalFrame);
            }
        }
        #endregion

        #region chartForm
        private bool _chartFormOpened;

        private void btnChart_Click(object sender, EventArgs e)
        {
            if (cbFileList.SelectedIndex < 0 || _chartFormOpened) return;
            string type = _coreProcess is VsPipePSNRProcess ? "PSNR" : "SSIM";
            FrmChart chart = new FrmChart(_fullData[cbFileList.SelectedIndex], _threshold, _frameRate[cbFPS.SelectedIndex], type);
            chart.Load   += (o, args) => _chartFormOpened = true;
            chart.Closed += (o, args) => _chartFormOpened = false;
            chart.Show();
        }
        #endregion

        #region cleanUpOption
        private static void RemoveScript(KeyValuePair<string, string> item)
        {
            try
            {
                File.Delete($"{item.Key}.lwi");
                File.Delete($"{item.Value}.lwi");
                File.Delete($"{item.Value}.vpy");
            }
            catch (Exception ex)
            {
                new Task(() => MessageBox.Show(ex.Message, @"RPChecker Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)).Start();
            }
        }


        private bool _remainFile;

        private void toolStripDropDownButton1_Click(object sender, EventArgs e)
        {
            _remainFile = !_remainFile;
            toolStripDropDownButton1.Image = _remainFile ? Resources.Checked : Resources.Unchecked;
        }

        private void toolStripDropDownButton1_MouseEnter(object sender, EventArgs e)
        {
            toolTip1.Show("保留中间文件", statusStrip1);
        }

        private void toolStripDropDownButton1_MouseLeave(object sender, EventArgs e)
        {
            toolTip1.RemoveAll();
        }
        #endregion

        #region statistics
        private void AddStatic()
        {
            try
            {
                RegistryStorage.RegistryAddCount(@"Software\RPChecker\Statistics", @"CheckedCount");
                var result = Regex.Match(toolStripStatusStdError.Text, @"Output (?<frame>\d+) frames in (?<second>[0-9]*\.?[0-9]+) seconds");
                if (!result.Success) return;
                var timespam = ToolKits.Second2Time(double.Parse(result.Groups["second"].Value));
                var frame = int.Parse(result.Groups["frame"].Value);
                RegistryStorage.RegistryAddTime(@"Software\RPChecker\Statistics", @"Time", timespam);
                RegistryStorage.RegistryAddCount(@"Software\RPChecker\Statistics", @"Frame", frame);
            }
            catch
            {
                // ignored
            }
        }

        private readonly int[] _poi = { 0, 10 };

        private void toolStripProgressBar1_Click(object sender, EventArgs e)
        {
            ++_poi[0];
            if (_poi[0] < 3 && _poi[1] == 10)
            {
                new Task(() => MessageBox.Show(@"Something happened", @"Something happened")).Start();
            }
            if (_poi[0] < _poi[1]) return;
            if (MessageBox.Show(@"是否打开关于界面", @"RPCheckerについて", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                Form2 version = new Form2();
                version.Show();
            }
            else
            {
                var frame = RegistryStorage.Load(@"Software\RPChecker\Statistics", @"Frame");
                var time  = RegistryStorage.Load(@"Software\RPChecker\Statistics", @"Time");
                new Task(() => MessageBox.Show(caption: @"Statistics",
                    text: $"总计帧数->[{frame}]<-{Environment.NewLine}" +
                          $"总计时间->[{time}]<-{Environment.NewLine}" +
                          $"平均帧率->{int.Parse(frame) / time.ToTimeSpan().TotalSeconds:F3}fps<-")).Start();
            }
            _poi[0]  = 00;
            _poi[1] += 10;
        }
        #endregion
    }

    public class ReSulT: INotifyPropertyChanged
    {
        private List<KeyValuePair<int, double>> _data;
        public List<KeyValuePair<int, double>> Data {
            get { return _data; }
            set {
                _data = value;
                NotifyPropertyChanged();
            }
        }
        public string FileName { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
