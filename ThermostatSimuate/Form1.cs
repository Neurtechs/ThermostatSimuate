using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using static ThermostatSimuate.GlobalVar;
using System.Data.SqlClient;
using DevExpress.Utils;
using DevExpress.XtraCharts;
using Microsoft.Win32;


namespace ThermostatSimuate
{
    public partial class Form1 : DevExpress.XtraEditors.XtraForm
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public Form1()
        {
            InitializeComponent();
        }
        private DateTime myTime;
        private string[] gNode;
        private double[] timeCooling;
        private double[] timeHeating;
        private DateTime[] timeStartCooling;
        private DateTime[] timeStartHeating;
        private double[] gradHeating;
        private double[] gradCooling;
        private double[] HWIndex; //current HW index
        private double[] index;  //c in y=mx+c
        private int[] on_off;
        private int[] sw_on_off;
        
        private int i;
        private DateTime myBaseTime;
        private DateTime prevTime;
        private int prevSec;
        private int nowSec;
        private double[] baseCooling; //50 minutes
        private double[] baseHeating;  //10 minutes
        private Random rand;
        private Series[] seriesC;
        private Series series1;
        private Series series2;
        private Series series3;
        private Series series4;
        private Series series5;

        private DataTable dt;

        private List<double> xValues;
        private List<double> yValues;
        private List<double> xValues2;
        private List<double> yValues2;
        private List<double> values120;
        private List<double> values60;
        private int points = 0;
        private bool[] skipFirstCalc;
        private bool chkChanged = false;
        private int[] forceOnOf;
        private int thermoStart;
        private int thermoEnd;

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Location = new Point(20, 20);
            int h = Screen.PrimaryScreen.WorkingArea.Height;
            int w = Screen.PrimaryScreen.WorkingArea.Width;
            this.Width = w - 100;
            this.Height = h - 100;
            thermoEnd = Convert.ToInt16(end.Text);
            thermoStart = Convert.ToInt16(start.Text);
            skipFirstCalc = new bool[21];
            comboBox1.SelectedIndex = 4;
            gNode = new string[21];
            timeCooling = new double[21];
            timeHeating = new double[21];
            timeStartCooling = new DateTime[21];
            timeStartHeating = new DateTime[21];
            gradCooling = new double[21];
            gradHeating = new double[21];
            HWIndex = new double[21];
            on_off = new int[21];
            sw_on_off = new int[21];
            index = new double[21];
            rand = new Random();
            xValues = new List<double>();
            yValues = new List<double>();
            baseCooling = new double[21];
            baseHeating = new double[21];
            forceOnOf= new int[21]; //0=off, 1=on, 3=inactive
            
            dateTimePicker1.Format = DateTimePickerFormat.Custom;
            dateTimePicker1.CustomFormat = "yyyy/MM/dd HH:mm:ss";

            int sec = dateTimePicker1.Value.Second;
            dateTimePicker1.Value = dateTimePicker1.Value.AddSeconds(-sec);
            int min = dateTimePicker1.Value.Minute;
            dateTimePicker1.Value = dateTimePicker1.Value.AddMinutes(-min);
            int hour = 18 - dateTimePicker1.Value.Hour;
            dateTimePicker1.Value = dateTimePicker1.Value.AddHours(+hour);

            dtpStartShift.Format = DateTimePickerFormat.Custom;
            dtpEndShift.Format = DateTimePickerFormat.Custom;
            dtpStartShift.CustomFormat = "HH:mm:ss";
            dtpEndShift.CustomFormat = "HH:mm:ss";

            dtpStartShift.Value = dateTimePicker1.Value.AddHours(1);
            dtpEndShift.Value= dateTimePicker1.Value.AddHours(3);
            Timer.Text = dateTimePicker1.Value.ToString();
            myTime = dateTimePicker1.Value; 
            myBaseTime = myTime;
            ResetData();
            SetChart();

            LoadGrid();

            for (int i = 20; i > 0; i--)
            {
                dt.Rows.Add();
                
            }
            for (int i = 1; i < 21; i++)
            {
                string myNode = Convert.ToString(i);
                if(myNode.Length == 1) { myNode = "0" + myNode; }
                gNode[i]=myNode;
                Double myRand = 0.8 + 0.5 * rand.NextDouble();
                baseCooling[i] = (60 * 50) * myRand;
                baseHeating[i] = (60 * 10) * myRand;
                timeCooling[i] = Math.Round(baseCooling[i] * (0.5 + rand.NextDouble()),0);
                timeHeating[i] = Math.Round(baseHeating[i] * (0.5 + rand.NextDouble()),0);
                gradCooling[i] = -5 / timeCooling[i];
                gradHeating[i] = 5 / timeHeating[i];
                forceOnOf[i] = 3;
                HWIndex[i] = 100;
                myRand = rand.NextDouble();
                skipFirstCalc[i] = true;
                on_off[i] = Convert.ToInt16(myRand);
                sw_on_off[i] = 1;
                index[i] = 95 + 5 * rand.NextDouble();
                listBoxControl1.Items.Add("Node " + gNode[i] + " Cooling time = " + Math.Round (timeCooling[i]/60,1) + ", Heating time = " +
                    Math.Round(timeHeating[i]/60,1) + " minutes");
               
                dt.Rows[i-1]["Node"] = gNode[i];
                dt.Rows[i-1]["Cooling"] = timeCooling[i];
                dt.Rows[i-1]["Heating"] = timeHeating[i];
                dt.Rows[i-1]["ThermoStatus"] = on_off[i];
                dt.Rows[i-1]["NodeStatus"] = 1;
                dt.Rows[i-1]["GradCooling"] = gradCooling[i];
                dt.Rows[i-1]["GradHeating"] = gradHeating[i];
                dt.Rows[i-1]["HWIndex"] = index[i];

            }

            //on_off[5] = 0;
            //dt.Rows[4]["ThermoStatus"] = 0;
            gridView1.Columns["Node"].SortOrder = DevExpress.Data.ColumnSortOrder.Descending;
        }
        private void SetChart()
        {
            ((XYDiagram)chartControl1.Diagram).EnableAxisXScrolling = true;
            ((XYDiagram)chartControl1.Diagram).AxisX.WholeRange.Auto = false;
            ((XYDiagram)chartControl1.Diagram).AxisX.WholeRange.SetMinMaxValues(0, 14400 / 60);
            ((XYDiagram)chartControl1.Diagram).AxisX.VisualRange.AutoSideMargins = false;
            ((XYDiagram)chartControl1.Diagram).AxisX.VisualRange.SetMinMaxValues(0, 7200 / 60);
            ((XYDiagram)chartControl1.Diagram).AxisX.VisualRange.Auto = false;


            ((XYDiagram)chartControl2.Diagram).EnableAxisXScrolling = true;
            ((XYDiagram)chartControl2.Diagram).AxisX.WholeRange.Auto = false;
            ((XYDiagram)chartControl2.Diagram).AxisX.WholeRange.SetMinMaxValues(0, 14400 / 60);
            ((XYDiagram)chartControl2.Diagram).AxisX.VisualRange.AutoSideMargins = false;
            ((XYDiagram)chartControl2.Diagram).AxisX.VisualRange.SetMinMaxValues(0, 7200 / 60);
            ((XYDiagram)chartControl2.Diagram).AxisX.VisualRange.Auto = false;
            ((XYDiagram)chartControl2.Diagram).AxisY.WholeRange.Auto = false;
            ((XYDiagram)chartControl2.Diagram).AxisY.WholeRange.SetMinMaxValues(2, 21);
        }
        private void ResetData()
        {
            dt = new DataTable();
            //da = new SqlDataAdapter(sql, mySqlConnection);

            //bu = new SqlCommandBuilder(da);
            //da.Fill(dt);
            dt.Columns.Add("Node");
            dt.Columns.Add("ThermoStatus");
            dt.Columns.Add("NodeStatus");
            dt.Columns.Add("Cooling");
            dt.Columns.Add("Heating");
            dt.Columns.Add("GradCooling");
            dt.Columns.Add("GradHeating");
            dt.Columns.Add("HWIndex");
        }
        private void LoadGrid()
        {
           
          
            gridControl1.DataSource = dt;
            gridView1.Columns[1].DisplayFormat.FormatType = FormatType.Numeric;
            gridView1.Columns[2].DisplayFormat.FormatType = FormatType.Numeric;
            gridView1.Columns[3].DisplayFormat.FormatType = FormatType.Numeric;
            gridView1.Columns[4].DisplayFormat.FormatType = FormatType.Numeric;
            gridView1.Columns[5].DisplayFormat.FormatType = FormatType.Numeric;
            gridView1.Columns[6].DisplayFormat.FormatType = FormatType.Numeric;
            gridView1.Columns[7].DisplayFormat.FormatType = FormatType.Numeric;
            gridView1.Columns[1].DisplayFormat.FormatString = "n2";
            gridView1.Columns[2].DisplayFormat.FormatString = "n2";
            gridView1.Columns[3].DisplayFormat.FormatString = "n2";
            gridView1.Columns[4].DisplayFormat.FormatString = "n2";
            gridView1.Columns[5].DisplayFormat.FormatString = "n2";
            gridView1.Columns[6].DisplayFormat.FormatString = "n2";
            gridView1.Columns[7].DisplayFormat.FormatString = "n2";
            
        }
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            timer1.Interval = 1000 / Convert.ToInt16(comboBox1.Text);
        }


        private async void timer1_Tick(object sender, EventArgs e)
        {
            i = 1;

          
            myTime = myTime.AddSeconds(1);
            nowSec += i;
            Timer.Text = myTime.ToString();
            double hMax = Convert.ToDouble(HIMax.Text);
            double hMin = Convert.ToDouble(HIMin.Text);
           
            //Thermostat section
            if (chkChanged == true)
            {
                if (chkShift.Checked == false)
                {
                    for (int j = 20; j > 0; j--)
                    {
                        timeStartCooling[j] = myTime;
                        timeStartHeating[j] = myTime;
                        index[j] = HWIndex[j];
                        dt.Rows[j - 1]["NodeStatus"] = 1;
                        sw_on_off[j] = 1;
                    }

                    chkChanged = false;
                }
            }
            if (chkShift.Checked == true) { goto ShiftHere;}
            for (int j = 20; j > 0; j--)           
            {
                if( on_off[j] == 1 && sw_on_off[j]==1)
                {
                    //Busy warming (Node on/Thermostat on)
                    TimeSpan duration = (myTime - timeStartHeating[j]);
                    double dur = Convert.ToInt32(duration.TotalSeconds);
                    dt.Rows[j - 1]["Heating"] = dur;                    
                    HWIndex[j] = index[j] + gradHeating[j] * (
                       dur);
                    dt.Rows[j - 1]["HWIndex"] = HWIndex[j];
                    dt.Rows[j - 1]["Cooling"] = 0;
                    //If switch changes, recalculate
                    //if (timeHeating[j] - dur <= 0)
                    if (HWIndex[j] >= thermoEnd)
                    {
                        //End of Heating reached - start to cool
                        index[j] = HWIndex[j];
                        on_off[j] = 0;
                        dt.Rows[j - 1]["ThermoStatus"] = 0;
                        //HWIndex[j] = 100;
                        
                        dt.Rows[j - 1]["HWIndex"] = HWIndex[j];
                        //Recalculate final heating gradient.
                        gradHeating[j] = (thermoEnd-thermoStart) / dur;
                        dt.Rows[j - 1]["GradHeating"] = gradHeating[j];
                        dt.Rows[j - 1]["Heating"] = 0;

                        //Values for the following cooling cycle

                        timeStartCooling[j] = myTime;
                        timeCooling[j] = Math.Round(baseCooling[j] * (0.8 + 0.4 * rand.NextDouble()), 0);
                        dt.Rows[j - 1]["Cooling"] = timeCooling[j];
                        gradCooling[j] = (thermoStart-thermoEnd) / timeCooling[j];
                        dt.Rows[j - 1]["GradCooling"] = gradCooling[j];

                        if (j == 5)
                        {
                            Log.Info("End of normal heating reached, turning thermostat off");
                            Log.Info("HWIndex[5] currently at = " + HWIndex[j]);
                            Log.Info("Set index[5] to HWIndex[5] = " + index[j]);
                            Log.Info("Line 259");
                        }

                    }
                }
                else if (on_off[j] == 0 || sw_on_off[j] == 0)
                {
                    //Normal cooling (Node on/Thermostat off)
                    TimeSpan duration = (myTime - timeStartCooling[j]);
                    double dur = Convert.ToInt32(duration.TotalSeconds);
                    dt.Rows[j - 1]["Cooling"] = dur;
                    HWIndex[j] = index[j] + gradCooling[j] * (
                       dur);
                    dt.Rows[j - 1]["HWIndex"] = HWIndex[j];
                    dt.Rows[j - 1]["Heating"] = 0;
                    if (HWIndex[j] <= thermoStart )
                    {
                        //End of cooling - start heating
                        index[j] = HWIndex[j];
                        on_off[j] = 1;
                        dt.Rows[j - 1]["ThermoStatus"] = 1;
                        //HWIndex[j] = 95;
                        dt.Rows[j - 1]["HWIndex"] = HWIndex[j];

                        //Recalculate final cooling gradient.
                        if (dur > 0)
                        {
                            gradCooling[j] = (thermoStart-thermoEnd) / dur;
                            dt.Rows[j - 1]["GradCooling"] = gradCooling[j];
                        }
                       
                        dt.Rows[j - 1]["Cooling"] = 0;

                        //Values for the following heating cycle

                        timeStartHeating[j] = myTime;
                        timeHeating[j] = Math.Round(baseHeating[j] * (0.8 + 0.4 * rand.NextDouble()), 0);
                        dt.Rows[j - 1]["Heating"] = timeHeating[j];
                        gradHeating[j] = (thermoEnd-thermoStart) / timeHeating[j];
                        dt.Rows[j - 1]["GradHeating"] = gradHeating[j];

                        if (j == 5)
                        {
                            Log.Info("End of normal cooling reached, turning thermostat on");
                            Log.Info("HWIndex[5] currently at = " + HWIndex[j]);
                            Log.Info("Set index[5] to HWIndex[5] = " + index[j]);
                            Log.Info("Line 301");
                        }
                    }
                }
            }
            ShiftHere: ;
            //Forced section
            if (chkShift.Checked == true)
            {
                //Load shedding on
                for (int j = 1; j < 21; j++)
                {
                    if (Convert.ToInt16(dt.Rows[j - 1]["ThermoStatus"]) == 0)
                    {
                        //Still cooling - so remain switched on
                        //Normal cooling
                        TimeSpan duration = (myTime - timeStartCooling[j]);
                        double dur = Convert.ToInt32(duration.TotalSeconds);
                        dt.Rows[j - 1]["Cooling"] = dur;
                        HWIndex[j] = index[j] + gradCooling[j] * (
                            dur);
                        dt.Rows[j - 1]["HWIndex"] = HWIndex[j];
                        dt.Rows[j - 1]["Heating"] = 0;
                        if (HWIndex[j] <= thermoStart)
                        {
                            sw_on_off[j] = 0;
                            on_off[j] = 1;
                           
                            dt.Rows[j - 1]["ThermoStatus"] = 1;
                            dt.Rows[j - 1]["NodeStatus"] = 0;
                            //Recalculate gradient
                            gradCooling[j] = (thermoStart - thermoEnd) / dur;
                            dt.Rows[j - 1]["GradCooling"] = gradCooling[j];
                            //[j] = HWIndex[j];
                            index[j] = HWIndex[j];
                            timeStartCooling[j] = myTime;
                            dt.Rows[j - 1]["Cooling"] = 0;

                        }
                        if (j == 5)
                        {
                            Log.Info("Load shedding in process but thermostat still off, so no change");
                            Log.Info("HWIndex[5] currently at = " + HWIndex[j]);
                            Log.Info("Line 320");
                        }
                    }
                    else  //Thermostat closed
                    {
                        if(sw_on_off[j] == 1 )  //node on
                        {
                            //Heating
                            //Get current curve
                            TimeSpan duration = (myTime - timeStartHeating[j]);
                            double dur = Convert.ToInt32(duration.TotalSeconds);
                            dt.Rows[j - 1]["Heating"] = dur;
                            HWIndex[j] = index[j] + gradHeating[j] * (
                                dur);
                            dt.Rows[j - 1]["HWIndex"] = HWIndex[j];
                            dt.Rows[j - 1]["Cooling"] = 0;

                            if (HWIndex[j] >= hMax)
                            {

                                //End of Heating reached - start to cool
                               
                                timeStartCooling[j] = myTime;
                                duration = (myTime - timeStartCooling[j]);
                                dur = Convert.ToInt32(duration.TotalSeconds);
                                timeCooling[j] = dur;
                                index[j] = HWIndex[j];
                                


                                sw_on_off[j] = 0;
                                dt.Rows[j - 1]["NodeStatus"] = 0;
                                if (j == 5)
                                {
                                    Log.Info("Load shedding in progress, end of heating cycle, switching off");
                                    Log.Info("HWIndex[5] currently at = " + HWIndex[j]);
                                    Log.Info("Set index[5] to HWIndex[5] = " + index[j]);
                                    Log.Info("Line 343");
                                }

                            }


                        }
                        else if (sw_on_off[j] == 0)
                        {
                            //Shedding cooling
                            TimeSpan duration = (myTime - timeStartCooling[j]);
                            double dur = Convert.ToInt32(duration.TotalSeconds);
                            dt.Rows[j - 1]["Cooling"] = dur;
                            HWIndex[j] = index[j] + gradCooling[j] * (
                                dur);
                            dt.Rows[j - 1]["HWIndex"] = HWIndex[j];
                            dt.Rows[j - 1]["Heating"] = 0;
                            if(HWIndex[j] <= hMin)
                            {
                                //End of cooling - start heating
                                index[j] = HWIndex[j];
                                sw_on_off[j] = 1;
                                dt.Rows[j - 1]["NodeStatus"] = 1;
                                dt.Rows[j - 1]["Cooling"] = 0;
                                timeStartHeating[j] = myTime;
                                duration = (myTime - timeStartHeating[j]);
                                dur = Convert.ToInt32(duration.TotalSeconds);
                                timeHeating[j] = dur;
                                
                                if (j == 5)
                                {
                                    Log.Info("End of forced cooling reached, turning switch on");
                                    Log.Info("HWIndex[5] currently at = " + HWIndex[j]);
                                    Log.Info("Set index[5] to HWIndex[5] = " + index[j]);
                                    Log.Info("Line 369");
                                }
                            }
                            //else
                            //{
                            //    //Busy warming (Node on/Thermostat on)
                            //    duration = (myTime - timeStartHeating[j]);
                            //    dur = Convert.ToInt32(duration.TotalSeconds);
                            //    dt.Rows[j - 1]["Heating"] = dur;
                            //    HWIndex[j] = index[j] + gradHeating[j] * (
                            //        dur);
                            //    dt.Rows[j - 1]["HWIndex"] = HWIndex[j];
                            //    dt.Rows[j - 1]["Cooling"] = 0;
                            //    if (HWIndex[j] >= Convert.ToInt16(HIMax.Text))
                            //    {
                            //        //End of Heating reached - start to cool
                            //        index[j] = HWIndex[j];
                            //        sw_on_off[j] = 0;
                            //        dt.Rows[j - 1]["NodeStatus"] = 0;
                            //        dt.Rows[j - 1]["Heating"] = 0;

                            //        if (j == 5)
                            //        {
                            //            Log.Info("End of normal heating reached, turning thermostat off");
                            //            Log.Info("HWIndex[5] currently at = " + HWIndex[j]);
                            //            Log.Info("Set index[5] to HWIndex[5] = " + index[j]);
                            //            Log.Info("Line 395");
                            //        }
                            //    }
                            //}
                        }
                    }
                }
            }

            Integrate(myTime);
        }
        private async void Integrate(DateTime myTime)
        {
            double diffSec = (myTime - prevTime).Hours * 3600 + (myTime - prevTime).Minutes * 60 + (myTime - prevTime).Seconds;
            double hMin = Convert.ToDouble(HIMin.Text);
            ((XYDiagram)chartControl1.Diagram).AxisY.WholeRange.SetMinMaxValues(hMin,100);

            if (nowSec > 7200)
            {
                
                ((XYDiagram)chartControl2.Diagram).AxisX.WholeRange.SetMinMaxValues(0, nowSec / 60);
                ((XYDiagram)chartControl2.Diagram).AxisX.VisualRange.SetMinMaxValues((nowSec - 7200) / 60, (nowSec + 7200) / 60);

                ((XYDiagram)chartControl1.Diagram).AxisX.WholeRange.SetMinMaxValues(0, nowSec / 60);
                ((XYDiagram)chartControl1.Diagram).AxisX.VisualRange.SetMinMaxValues((nowSec - 7200) / 60, (nowSec + 14400) / 60);


            }
            xValues.Add(nowSec);
         
            //values60.Add(currentDemand);
            //values120.Add(currentDemand);
            int i = Convert.ToInt16(comboBox1.Text);
            int gnode  =0;
            int status = 0;



         
            //if (myTime.Second == 0 || myTime.Second == 30)
            if(myTime.Second % 60 ==0) //every 5 seconds
            {
                points += 1;
                if (points > 230)
                {
                    series1.Points.RemoveAt(0);
                    series2.Points.RemoveAt(0);
                    series3.Points.RemoveAt(0);
                    series4.Points.RemoveAt(0);
                    series5.Points.RemoveAt(0);
                }
                series1.Points.Add(new SeriesPoint(nowSec / 60, HWIndex[1]));
                series2.Points.Add(new SeriesPoint(nowSec / 60, HWIndex[5]));
                series3.Points.Add(new SeriesPoint(nowSec / 60, HWIndex[10]));
                series4.Points.Add(new SeriesPoint(nowSec / 60, HWIndex[15]));
                series5.Points.Add(new SeriesPoint(nowSec / 60, HWIndex[20]));

                for (int j = 1; j < 21; j++)
                {
                    
                    bool remove = false;
                    gnode = Convert.ToInt16(gNode[j]);
                    if (points >230){ remove = true;}
                    status = on_off[j];
                    if (on_off[j] == 1 && sw_on_off[j] ==1) { status = 1; }
                    else { status = 0; }
                  
                    if (remove == true) { seriesC[gnode].Points.RemoveAt(0); }

                    if (status == 1)
                    {
                        seriesC[gnode].Points.Add(new SeriesPoint(nowSec / 60, gnode));
                    }
                    else
                    {
                        seriesC[gnode].Points.Add(new SeriesPoint(nowSec / 60, 0));

                    }
                }
                prevSec = 0;
                prevTime = myTime;
               
                xValues.Clear();
                yValues.Clear();
            }
        }
        private void buttonStart_Click(object sender, EventArgs e)
        {
            myTime = dateTimePicker1.Value;
            prevTime = myTime;
            seriesC = new Series[21];
            seriesC[1] = chartControl2.GetSeriesByName("1");
            seriesC[2] = chartControl2.GetSeriesByName("2");
            seriesC[3] = chartControl2.GetSeriesByName("3");
            seriesC[4] = chartControl2.GetSeriesByName("4");
            seriesC[5] = chartControl2.GetSeriesByName("5");
            seriesC[6] = chartControl2.GetSeriesByName("6");
            seriesC[7] = chartControl2.GetSeriesByName("7");
            seriesC[8] = chartControl2.GetSeriesByName("8");
            seriesC[9] = chartControl2.GetSeriesByName("9");
            seriesC[10] = chartControl2.GetSeriesByName("10");
            seriesC[11] = chartControl2.GetSeriesByName("11");
            seriesC[12] = chartControl2.GetSeriesByName("12");
            seriesC[13] = chartControl2.GetSeriesByName("13");
            seriesC[14] = chartControl2.GetSeriesByName("14");
            seriesC[15] = chartControl2.GetSeriesByName("15");
            seriesC[16] = chartControl2.GetSeriesByName("16");
            seriesC[17] = chartControl2.GetSeriesByName("17");
            seriesC[18] = chartControl2.GetSeriesByName("18");
            seriesC[19] = chartControl2.GetSeriesByName("19");
            seriesC[20] = chartControl2.GetSeriesByName("20");
            series1 = new Series();
            series1 = chartControl1.GetSeriesByName("DEHW 1");
            series1.Points.Clear();
            series2 = new Series();
            series2 = chartControl1.GetSeriesByName("DEHW 5");
            series2.Points.Clear();
            series3 = new Series();
            series3 = chartControl1.GetSeriesByName("DEHW 10");
            series3.Points.Clear();
            series4 = new Series();
            series4 = chartControl1.GetSeriesByName("DEHW 15");
            series4.Points.Clear();
            series5 = new Series();
            series5 = chartControl1.GetSeriesByName("DEHW 20");
            series5.Points.Clear();


            for (int i = 20; i > 0; i--)
            {
                timeStartCooling[i] = myTime;
                timeStartHeating[i] = myTime;
                seriesC[i].Points.Clear();
            }
            chkChanged = false;
            timer1.Start();
        }



        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            dateTimePicker1.CustomFormat = "yyyy/MM/dd HH:mm:ss";
            int min = dateTimePicker1.Value.Minute;
            // dateTimePicker1.Value = dateTimePicker1.Value.AddSeconds(-min);
            int sec = dateTimePicker1.Value.Second;
            dateTimePicker1.Value = dateTimePicker1.Value.AddSeconds(-sec);
            Timer.Text = dateTimePicker1.Value.ToString();
            series1=new Series();
            series1 = chartControl1.GetSeriesByName("DEHW 1");
            series1.Points.Clear();
            series2 = new Series();
            series2 = chartControl1.GetSeriesByName("DEHW 5");
            series2.Points.Clear();
            series3 = new Series();
            series3 = chartControl1.GetSeriesByName("DEHW 10");
            series3.Points.Clear();
            series4 = new Series();
            series4 = chartControl1.GetSeriesByName("DEHW 15");
            series4.Points.Clear();
            series5 = new Series();
            series5 = chartControl1.GetSeriesByName("DEHW 20");
            series5.Points.Clear();


            seriesC = new Series[22];

            seriesC[1] = chartControl2.GetSeriesByName("1");
            seriesC[2] = chartControl2.GetSeriesByName("2");
            seriesC[3] = chartControl2.GetSeriesByName("3");
            seriesC[4] = chartControl2.GetSeriesByName("4");
            seriesC[5] = chartControl2.GetSeriesByName("5");
            seriesC[6] = chartControl2.GetSeriesByName("6");
            seriesC[7] = chartControl2.GetSeriesByName("7");
            seriesC[8] = chartControl2.GetSeriesByName("8");
            seriesC[9] = chartControl2.GetSeriesByName("9");
            seriesC[10] = chartControl2.GetSeriesByName("10");
            seriesC[11] = chartControl2.GetSeriesByName("11");
            seriesC[12] = chartControl2.GetSeriesByName("12");
            seriesC[13] = chartControl2.GetSeriesByName("13");
            seriesC[14] = chartControl2.GetSeriesByName("14");
            seriesC[15] = chartControl2.GetSeriesByName("15");
            seriesC[16] = chartControl2.GetSeriesByName("16");
            seriesC[17] = chartControl2.GetSeriesByName("17");
            seriesC[18] = chartControl2.GetSeriesByName("18");
            seriesC[19] = chartControl2.GetSeriesByName("19");
            seriesC[20] = chartControl2.GetSeriesByName("20");
            seriesC[21] = chartControl2.GetSeriesByName("21");

            for (int j = 1; j < 21; j++)
            {
                seriesC[j].Points.Clear();
            }

        }

        private void buttonPause_Click(object sender, EventArgs e)
        {
            timer1.Stop();
        }

        private void buttonResume_Click(object sender, EventArgs e)
        {
            timer1.Start();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private async void chkShift_CheckedChanged(object sender, EventArgs e)
        {
            chkChanged = true;
        }

        private void chkShift_CheckStateChanged(object sender, EventArgs e)
        {
            //if (chkShift.Checked = false)
            //{
            //    for (int j = 20; j > 0; j--)
            //    {
            //        sw_on_off[j] = 1;
            //        if (on_off[j] == 1)
            //        {
            //            dt.Rows[j - 1]["NodeStatus"] = 1;
            //            ignoreOn[j] = false;
            //            index[j] = Convert.ToInt16(HIMin.Text);
            //            timeStartHeating[j] = myTime;
            //        }
            //    }
            //}

        }

        private void start_EditValueChanged(object sender, EventArgs e)
        {
            
            thermoStart = Convert.ToInt16(start.Text);
        }

        private void end_EditValueChanged(object sender, EventArgs e)
        {
            thermoEnd = Convert.ToInt16(end.Text);
           
        }

        private void HIMax_EditValueChanged(object sender, EventArgs e)
        {

        }
    }
}
