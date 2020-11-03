using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.IO;
using DataProcessor.Json;
using System.Threading;
using System.Windows.Threading;

namespace DataProcessor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public struct SpeedObject
    {
        public int startIndex;
        public int Max_speed;

        public SpeedObject(int i, int speed)
        {
            startIndex = i;
            Max_speed = speed;
        }
    };

    public partial class MainWindow : Window
    {
        private List<string> SourceDirectories = new List<string>();
        private int nTotalDeal = 0;
        private int nTradedCount = 0;
        private const string FileTag = "_rod.geojson";
        private const string FileTag_2 = "_shz.geojson";
        public MainWindow()
        {
            InitializeComponent();

            DispatcherTimer myDispatcherTimer = new DispatcherTimer();

            myDispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            myDispatcherTimer.Tick += new EventHandler(UpdateProgress);

            myDispatcherTimer.Start();
        }

        private void UpdateProgress(object o, EventArgs sender)
        {
            prgProcess.Value = nTradedCount;
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();

            dlg.Description = "Select the Map data path...";

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txbPath.Text = dlg.SelectedPath;
            }
        }

        private void BtnProcess_Click(object sender, RoutedEventArgs e)
        {
            SourceDirectories.Clear();
            nTotalDeal = 0;
            DirectoryInfo di = new DirectoryInfo(txbPath.Text);
            foreach (System.IO.DirectoryInfo subDirectory in di.GetDirectories())
            {
                SourceDirectories.Add(subDirectory.Name);
            }

            prgProcess.Value = 0;
            prgProcess.Minimum = nTradedCount = 0;
            prgProcess.Maximum = SourceDirectories.Count;

            Thread thread = new Thread(new ParameterizedThreadStart(ProcessConvert));
            thread.Start(txbPath.Text);

        }

        private void ProcessConvert(object param)
        {
            string path = (string)param;
            string targetPath = path + "_Convert";
            Directory.CreateDirectory(targetPath);
            
            foreach(string subPath in SourceDirectories)
            {
                string subDirectoryPath = targetPath + "\\" + subPath;
                Directory.CreateDirectory(subDirectoryPath);

                nTradedCount ++;

                ConvertFile(path + "\\" + subPath + "\\" + subPath + FileTag, subDirectoryPath + "\\" + subPath + FileTag);
                File.Copy(path + "\\" + subPath + "\\" + subPath + FileTag_2, subDirectoryPath + "\\" + subPath + FileTag_2, true);
            }
        }

        private double GetHeading(JArray gemtryCoordinates)
        {
            double lon1 = ((JArray)gemtryCoordinates[0])[0].AsNumber();
            double lat1 = ((JArray)gemtryCoordinates[0])[1].AsNumber();
            double lon2 = ((JArray)gemtryCoordinates[gemtryCoordinates.Count-1])[0].AsNumber();
            double lat2 = ((JArray)gemtryCoordinates[gemtryCoordinates.Count - 1])[1].AsNumber();

            return Math.Atan2(  Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(lon2 - lon1),
                                Math.Sin(lon2 - lon1) * Math.Cos(lat2) );
        }

        public double ConvertToRadians(double angle)
        {
            return (Math.PI / 180) * angle;
        }

        public double ConvertToAngle(double radian)
        {
            return  radian * 180 / Math.PI ;
        }

        public double AngleDifference(double angle1, double angle2)
        {
            return Math.Min(Math.Abs(angle1 - angle2), 360 - Math.Abs(angle1 - angle2));
        }

        private void ConvertFile(string sourcePath, string targetPath)
        {
            List<int> GroupIndexes = new List<int>();
            List<int> NewGroupInds = new List<int>();
            JObject obj = JObject.Parse(File.ReadAllText(sourcePath,Encoding.UTF8));

            JArray jArrFeatures = (JArray)obj["features"];

            for (int i = 0; i < jArrFeatures.Count; i++)
            {
                if (jArrFeatures[i]["geometry"]["type"].AsString().Equals("MultiLineString"))
                    jArrFeatures[i]["properties"]["Heading"] = ConvertToAngle(GetHeading( (JArray)((JArray)jArrFeatures[i]["geometry"]["coordinates"])[0]));
                else
                    jArrFeatures[i]["properties"]["Heading"] = ConvertToAngle(GetHeading((JArray)jArrFeatures[i]["geometry"]["coordinates"]));
            }

            for(int i = 0; i < jArrFeatures.Count - 1; i++)
            {
                for(int j = i + 1; j < jArrFeatures.Count; j++)
                {
                    int compareValue = jArrFeatures[i]["properties"]["ROAD_NAME"].AsString().CompareTo(jArrFeatures[j]["properties"]["ROAD_NAME"].AsString());
                    if (compareValue < 0) continue;
                    if (compareValue > 0)
                    {
                        JObject objTemp = jArrFeatures[i];
                        jArrFeatures[i] = jArrFeatures[j];
                        jArrFeatures[j] = objTemp;
                        continue;
                    }

                    int lane_i = int.Parse(jArrFeatures[i]["properties"]["LANES"].AsString());
                    int lane_j = int.Parse(jArrFeatures[j]["properties"]["LANES"].AsString());

                    if (lane_i > lane_j) continue;
                    if (lane_i < lane_j)
                    {
                        JObject objTemp = jArrFeatures[i];
                        jArrFeatures[i] = jArrFeatures[j];
                        jArrFeatures[j] = objTemp;
                        continue;
                    }

                    int MaxSpeed_i = int.Parse(jArrFeatures[i]["properties"]["Max_Speed"].AsString());
                    int MaxSpeed_j = int.Parse(jArrFeatures[j]["properties"]["Max_Speed"].AsString());

                    if (MaxSpeed_i > MaxSpeed_j) continue;
                    if (MaxSpeed_i < MaxSpeed_j)
                    {
                        JObject objTemp = jArrFeatures[i];
                        jArrFeatures[i] = jArrFeatures[j];
                        jArrFeatures[j] = objTemp;
                        continue;
                    }

                    /*double Heading_i = jArrFeatures[i]["properties"]["Heading"].AsNumber();
                    double Heading_j = jArrFeatures[j]["properties"]["Heading"].AsNumber();

                    if (Heading_i < Heading_j) continue;
                    if (Heading_i > Heading_j)
                    {
                        JObject objTemp = jArrFeatures[i];
                        jArrFeatures[i] = jArrFeatures[j];
                        jArrFeatures[j] = objTemp;
                        continue;
                    }*/
                }
            }

            GroupIndexes.Add(0);
            NewGroupInds.Add(0);
            for (int i = 0; i < jArrFeatures.Count(); i ++)
            {
                int last = GroupIndexes[GroupIndexes.Count - 1];
                
                if (!jArrFeatures[last]["properties"]["ROAD_NAME"].AsString().Equals(jArrFeatures[i]["properties"]["ROAD_NAME"].AsString()))
                {
                    GroupIndexes.Add(i);
                    NewGroupInds.Add(i);
                }
            }
            for (int i = 0; i < NewGroupInds.Count - 1; i++)
            {
                for (int j = i + 1; j < NewGroupInds.Count; j++)
                {
                    int ind_i = NewGroupInds[i];
                    int ind_j = NewGroupInds[j];

                    int lane_i = int.Parse(jArrFeatures[ind_i]["properties"]["LANES"].AsString());
                    int lane_j = int.Parse(jArrFeatures[ind_j]["properties"]["LANES"].AsString());

                    int Speed_i = int.Parse(jArrFeatures[ind_i]["properties"]["Max_Speed"].AsString());
                    int Speed_j = int.Parse(jArrFeatures[ind_j]["properties"]["Max_Speed"].AsString());

                    if (lane_i < lane_j || (lane_i == lane_j && Speed_i < Speed_j))
                    {
                        NewGroupInds[i] = ind_j;
                        NewGroupInds[j] = ind_i;
                    }
                }
            }
            JArray jSpeedTurnedFeatures = new JArray();

            for (int i = 0; i < NewGroupInds.Count; i ++)
            {
                int index = GroupIndexes.IndexOf(NewGroupInds[i]);
                if (index >= 0)
                {
                    int last;
                    if (index == GroupIndexes.Count - 1)
                        last = jArrFeatures.Count;
                    else
                        last = GroupIndexes[index + 1];
                    for (int j = NewGroupInds[i]; j < last; j++)
                        jSpeedTurnedFeatures.Add(jArrFeatures[j]);
                }
            }
            obj["features"] = jSpeedTurnedFeatures;

            jArrFeatures = (JArray)obj["features"];

            if (jArrFeatures.Count > 0)
            {
                JArray jTurnedFeatures = new JArray();
                JObject jFeature = jArrFeatures[0];
                jTurnedFeatures.Add(jFeature);
                jArrFeatures.Remove(jFeature);
                while (jArrFeatures.Count > 0)
                {
                    JObject jAddedFeature = null;
                    double CurrentAngle = jFeature["properties"]["Heading"].AsNumber();
                    for (int i = 0; i < jArrFeatures.Count; i++)
                    {
                        if (!jFeature["properties"]["ROAD_NAME"].AsString().Equals(jArrFeatures[i]["properties"]["ROAD_NAME"].AsString()))
                            continue;
                       /* if (!jFeature["properties"]["LANES"].AsString().Equals(jArrFeatures[i]["properties"]["LANES"].AsString()))
                            continue;*/

                        if (jFeature["properties"]["T_Node"].AsString().Equals(jArrFeatures[i]["properties"]["F_Node"].AsString()))
                        {
                            if (jAddedFeature == null)
                                jAddedFeature = jArrFeatures[i];
                            else
                            {
                                double SelectedAngle = jAddedFeature["properties"]["Heading"].AsNumber();
                                double NewAngle = jArrFeatures[i]["properties"]["Heading"].AsNumber();
                                if (AngleDifference(SelectedAngle, CurrentAngle) > AngleDifference(NewAngle, CurrentAngle))
                                {
                                    jAddedFeature = jArrFeatures[i];
                                }
                            }
                        }
                    }
                    if (jAddedFeature != null)
                    {
                        jTurnedFeatures.Add(jAddedFeature);
                        jArrFeatures.Remove(jAddedFeature);

                        jFeature = jAddedFeature;
                    }
                    else
                    {
                        
                        jFeature = jArrFeatures[0];
                        jTurnedFeatures.Add(jFeature);
                        jArrFeatures.Remove(jFeature);
                    }
                }
                obj["features"] = jTurnedFeatures;
            }

            jArrFeatures = (JArray)obj["features"];
            foreach(JObject jFeature in jArrFeatures)
            {
                jFeature["properties"]["Heading"] = ((int)jFeature["properties"]["Heading"].AsNumber());
            }

            File.WriteAllText(targetPath, obj.ToString(), Encoding.UTF8);
        }
    }
}
