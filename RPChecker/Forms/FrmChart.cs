﻿using System;
using RPChecker.Util;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization.Charting;
using System.Linq;

namespace RPChecker.Forms
{
    public partial class FrmChart : Form
    {
        private readonly ReSulT _info = new ReSulT();
        private readonly int _threshold;
        private readonly double _fps;
        private readonly string _type;
        public FrmChart(ReSulT info, int threshold, double fps, string type)
        {
            InitializeComponent();
            _info.FileName = info.FileName;
            _info.Data = info.Data;
            //_info.PropertyChanged += (sender, args) => DrawChart();
            _threshold = threshold;
            _fps = fps;
            _type = type;
        }

        private void FrmChart_Load(object sender, EventArgs e)
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Point saved = ToolKits.String2Point(RegistryStorage.Load(@"Software\RPChecker", "ChartLocation"));
            if (saved != new Point(-32000, -32000)) Location = saved;
            DrawChart();
        }

        private void DrawChart()
        {
            chart1.Series.Clear();
            Series series1 = new Series(_type)
            {
                Color = Color.Blue,
                ChartType = SeriesChartType.Line,
                IsValueShownAsLabel = false
            };

            Series series2 = new Series("frame")
            {
                Color = Color.Red,
                ChartType = SeriesChartType.Point,
                IsValueShownAsLabel = false
            };
            int interval = (int) Math.Round(_fps) * 30;
            var task = new Task(() =>
            {
                foreach(var frame in _info.Data.OrderBy(item => item.Key))
                {
                    series1.Points.AddXY(frame.Key, frame.Value);
                    if ((frame.Key + 1) % interval == 0)
                    {
                        Invoke(new Action(() => chart1.ChartAreas[0].AxisX.CustomLabels.Add(frame.Key - 20, frame.Key + 20,
                            $"{TimeSpan.FromSeconds(Math.Round(frame.Key / _fps)):mm\\:ss}")));
                    }
                    if (frame.Value < _threshold)
                    {
                        series2.Points.AddXY(frame.Key, frame.Value);
                    }
                }
            });
            task.ContinueWith(t =>
            {
                Invoke(new Action(() =>
                {
                    chart1.Series.Add(series1);
                    chart1.Series.Add(series2);
                }));
            });
            task.Start();
        }

        private void FrmChart_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                RegistryStorage.Save(Location.ToString(), @"Software\RPChecker", "ChartLocation");
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void btnSaveAsImage_Click(object sender, EventArgs e)
        {
            var rnd = Path.GetRandomFileName().Substring(0, 8).ToUpper();
            var fileName = Path.Combine(Path.GetDirectoryName(_info.FileName) ?? "", $"{rnd}.png");
            chart1.SaveImage(fileName, ChartImageFormat.Png);
        }
    }
}
