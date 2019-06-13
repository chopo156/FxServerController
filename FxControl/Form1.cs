﻿using FxControl.Properties;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FxControl
{
    public partial class Form1 : Form
    {
        private Process _process;

        DataTable dt, dt2;
        int count;
        string argument;

        private Boolean _isFivemServerRunning, ServerStarted;

        string date;

        public Form1()
        {
            InitializeComponent();
            var dataSource = new List<Language>
            {
                new Language() { Name = "Français", Value = "fr-FR" },
                new Language() { Name = "English", Value = "en-US" },
                new Language() { Name = "Türkçe", Value = "tr-TR" }
            };
            cmbSelectLanguage.DataSource = dataSource;
            cmbSelectLanguage.DisplayMember = "Name";
            cmbSelectLanguage.ValueMember = "Value";
            for (var i = 0; i < dataSource.Count; i++)
            {
                if (dataSource[i].Value == Settings.Default.ApplicationLanguage)
                {
                    cmbSelectLanguage.SelectedIndex = i;
                }
            }
        }

        private void Fxselectlocation()
        {
            if (string.IsNullOrWhiteSpace(Settings.Default.ServerExeLocation))
            {
                OpenFileDialog fx = new OpenFileDialog
                {
                    Title = "",
                    Filter = "FXServer|FXServer.exe"
                };
                if (fx.ShowDialog() == DialogResult.OK)
                {
                    Settings.Default.ServerExeLocation = fx.FileName;
                    Settings.Default.Save();
                    txtServerExeLocation.Text = Settings.Default.ServerExeLocation;
                }
                else
                {
                    this.Close();
                }
            }
            else
            {
                txtServerExeLocation.Text = Settings.Default.ServerExeLocation;
            }
        }

        private void Configselectlocation()
        {
            if (string.IsNullOrWhiteSpace(Settings.Default.ServerConfigLocation))
            {
                OpenFileDialog conf = new OpenFileDialog
                {
                    Title = "",
                    Filter = "Config|server.cfg"
                };
                if (conf.ShowDialog() == DialogResult.OK)
                {
                    Settings.Default.ServerConfigLocation = conf.FileName;
                    Settings.Default.Save();
                    txtServerCfgLocation.Text = Settings.Default.ServerConfigLocation;
                }
                else
                {
                    Close();
                }
            }
            else
            {
                txtServerCfgLocation.Text = Settings.Default.ServerConfigLocation;
            }
        }

        private void SelectLogsLocation()
        {
            FolderBrowserDialog loglocation = new FolderBrowserDialog
            {
                SelectedPath = Settings.Default.ServerLogLocation,
                ShowNewFolderButton = true,
                Description = ""
            };

            if (loglocation.ShowDialog() == DialogResult.OK)
            {
                Settings.Default.ServerLogLocation = loglocation.SelectedPath;
                Settings.Default.Save();
                txtServerLogsLocation.Text = loglocation.SelectedPath;
            }
        }

        private void SetupServerLog()
        {
            if (string.IsNullOrWhiteSpace(Settings.Default.ServerLogLocation))
            {
                string path = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                Settings.Default.ServerLogLocation = path;
                Settings.Default.Save();
                txtServerLogsLocation.Text = Settings.Default.ServerLogLocation;
            }
            else
            {
                txtServerLogsLocation.Text = Settings.Default.ServerLogLocation;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        { 
            if (Settings.Default.OneSyncCheck)
            {
                oneSyncCheck.Checked = true;
            }
            else
            {
                oneSyncCheck.Checked = true;
                oneSyncCheck.Checked = false;
            }
            dt = new DataTable();
            dt2 = new DataTable();
            dt.Columns.Add("resource");
            dt2.Columns.Add("name");
            dt2.Columns.Add("id");
            dt2.Columns.Add("ping");
            CheckForIllegalCrossThreadCalls = false;
            Fxselectlocation();
            Configselectlocation();
            SetupServerLog();
            serveripsave();
            chckBoxClearCache.Checked = Settings.Default.cache;
            chckBoxEnableServerLogs.Checked = Settings.Default.EnableServerLogs;
            txtServerIp.Text = Settings.Default.ServerIP;
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            for (int i = 0; i < lstBoxTiming.Items.Count; i++)
            {
                if (DateTime.Now.ToString("HH:mm:ss") == lstBoxTiming.Items[i].ToString())
                {
                    timer1.Stop();
                    OnRestart();
                }
            }

            if (_isFivemServerRunning)
            {
                btnStartServer.Enabled = false;
                btnStopServer.Enabled = true;
                btnRestartServer.Enabled = true;
                btnChangeServerLocation.Enabled = false;
                btnChangeConfigLocation.Enabled = false;
                cmbSelectLanguage.Enabled = false;
                groupBox5.Enabled = true;
                CheckIfCrashed();
                count++;
                if (count == 10)
                {
                    count = 0;
                    Thread addDatathread = new Thread(async () =>
                    await serverInfo());
                    addDatathread.Start();
                    Thread.Sleep(500);
                }
            }
            else
            {
                btnStartServer.Enabled = true;
                btnStopServer.Enabled = false;
                btnRestartServer.Enabled = false;
                btnChangeServerLocation.Enabled = true;
                btnChangeConfigLocation.Enabled = true;
                cmbSelectLanguage.Enabled = true;
                groupBox5.Enabled = false;
                progressBar1.Value = 0;
                btnStartServer.BackColor = Color.Transparent;
            }

        }

        private void StopServer()
        {
            try
            {
                count = 11;
                for (int i = 0; i < dataGridView2.RowCount; i++)
                {
                    Ress("clientkick", dataGridView2.Rows[i].Cells[1].Value.ToString(), "Sunucu Bakimda");
                }
                Thread.Sleep(1000);
                count = 0;
                _process.Kill();
                _isFivemServerRunning = false;
                dt.Clear();
            }
            catch (Exception)
            {
            }
        }

        private void OnRestart()
        {
            StopServer();
            Thread.Sleep(5000);
            StartServer();
            timer1.Start();
        }

        public void CheckIfCrashed()
        {
            if (!_isFivemServerRunning) return;

            if (!IsProcessRunning())
            {
                StartServer();
                return;
            }
        }

        private bool IsProcessRunning()
        {
            if (_process == null)
            {
                return false;
            }

            try
            {
                Process.GetProcessById(_process.Id);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return true;
        }

        public void StartServer()
        {
            string path = Settings.Default.ServerLogLocation + "/Logs/";
            if (Settings.Default.EnableServerLogs && !Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            ServerStarted = false;
            string serverPath = Settings.Default.ServerConfigLocation.Replace("server.cfg", "");
            DirectoryInfo dir = new DirectoryInfo(@serverPath + "cache");
            if (dir.Exists && chckBoxClearCache.Checked)
            {
                try
                {
                    dir.Delete(true);
                }
                catch (Exception)
                {

                }
                Thread.Sleep(3000);
            }
            _process = new Process();

            var startInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = Settings.Default.ServerExeLocation,        
                Arguments = argument, 
                WorkingDirectory = Settings.Default.ServerConfigLocation.Replace("\\server.cfg", "")                    
            };
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            _process.StartInfo = startInfo;
            _process.StartInfo.RedirectStandardInput = true;
            date = DateTime.Now.ToString("yyyy_MM_dd_HH_mm");
            _process.OutputDataReceived += CaptureOutput;
            _process.Start();
            _process.BeginOutputReadLine();
            _isFivemServerRunning = true;
        }

        //started resource
        private void CaptureOutput(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                Invoke(new Action(() => richTxtLogScreen.AppendText(Environment.NewLine + e.Data)));
                Invoke(new Action(() => richTxtLogScreen.ScrollToCaret()));
                if (e.Data == "Server license key authentication succeeded. Welcome!")
                {
                    ServerStarted = true;
                    progressBar1.Value = progressBar1.Maximum;
                    btnStartServer.BackColor = Color.Green;

                }
                else if (e.Data.Contains("Couldn't") || e.Data.Contains("Error"))
                {
                    Invoke(new Action(() => errorText.AppendText(Environment.NewLine + e.Data)));
                    Invoke(new Action(() => errorText.ScrollToCaret()));
                }
                else if (e.Data.Contains("Started resource"))
                {
                    //e.Data.Replace("started resource").Trim()
                    //  dataGridView1.Rows.Add(e.Data.Replace("Started resource", "").Trim());
                    //AddData(e.Data.Replace("Started resource", "").Trim());
                    Thread addDatathread = new Thread(() =>
                    AddData(e.Data.Replace("Started resource", "").Trim()));
                    addDatathread.Start();
                }
                if (!ServerStarted)
                {
                    if (progressBar1.Value < progressBar1.Maximum)
                    {
                        progressBar1.Value += 1;
                    }
                    else
                    {
                        progressBar1.Maximum += 100;
                    }
                }
                if (Settings.Default.EnableServerLogs)
                {
                    try
                    {
                        File.AppendAllText(Settings.Default.ServerLogLocation + "/Logs/" + date + ".txt", e.Data.ToString() + "\n");

                    }
                    catch (Exception)
                    { 
                    }
                }
            }
        }

        private void Ress(string status, string resourcename, string reason)
        {
            StreamWriter myStreamWriter = _process.StandardInput;
            myStreamWriter.WriteLine(status + " " + resourcename + " " + reason);
            myStreamWriter.Flush();
        }

        private void Button10_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtSendCommand.Text) && !string.IsNullOrWhiteSpace(comboBox1.Text))
            {
                Ress(comboBox1.Text, txtSendCommand.Text, "");
            }
        }

        private void Button9_Click(object sender, EventArgs e)
        {
            StreamWriter myStreamWriter = _process.StandardInput;
            myStreamWriter.WriteLine("refresh");
            myStreamWriter.Flush();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                _process.Kill();
            }
            catch (Exception)
            {

            }
        }

        private void TextBox2_TextChanged(object sender, EventArgs e)
        {
            try
            {
                ((DataTable)dataGridView1.DataSource).DefaultView.RowFilter = string.Format("resource like '%{0}%'", txtSearchResource.Text.Trim().Replace("'", "''"));
            }
            catch (Exception)
            {
            }
        }

        private void TextBox2_TextChanged_1(object sender, EventArgs e)
        {

        }

        private void StopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count > 0)
            {
                Ress("stop", dataGridView1.SelectedRows[0].Cells[0].Value.ToString(), "");
                dataGridView1.Rows.Remove(dataGridView1.SelectedRows[0]);
            }
        }

        private void RestartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Ress("restart", dataGridView1.SelectedRows[0].Cells[0].Value.ToString(), "");
            dataGridView1.Rows.Remove(dataGridView1.SelectedRows[0]);
        }

        private void BtnChangeServerLocation_Click(object sender, EventArgs e)
        {
            Settings.Default.ServerExeLocation = null;
            Settings.Default.Save();
            Fxselectlocation();
        }

        private void BtnChangeConfigLocation_Click(object sender, EventArgs e)
        {
            Settings.Default.ServerConfigLocation = null;
            Settings.Default.Save();
            Configselectlocation();
        }

        private void BtnAddTimeToList_Click(object sender, EventArgs e)
        {
            lstBoxTiming.Items.Add(dateTimePicker1.Value.ToString("HH:mm:ss"));
        }

        private void BtnDeleteSelectedTime_Click(object sender, EventArgs e)
        {
            if (lstBoxTiming.SelectedItems.Count > 0)
            {
                for (int i = 0; i < lstBoxTiming.SelectedItems.Count; i++)
                {
                    lstBoxTiming.Items.Remove(lstBoxTiming.SelectedItems[i]);
                }
            }
        }

        private void BtnClearTimerList_Click(object sender, EventArgs e)
        {
            lstBoxTiming.Items.Clear();
        }

        private void Button8_Click(object sender, EventArgs e)
        {

        }

        private void BtnStartServer_Click(object sender, EventArgs e)
        {
            StartServer();
            progressBar1.Maximum = 100;
        }

        private void BtnStopServer_Click(object sender, EventArgs e)
        {
            richTxtLogScreen.Text = string.Empty;
            StopServer();
        }

        private void BtnRestartServer_Click(object sender, EventArgs e)
        {
            richTxtLogScreen.Text = string.Empty;
            OnRestart();
        }

        private void ChckBoxClearCache_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.cache = chckBoxClearCache.Checked;
            Settings.Default.Save();
        }

        private void BtnChangeServerLogsLocation_Click(object sender, EventArgs e)
        {
            SelectLogsLocation();
        }

        private void BtnShowLogs_Click(object sender, EventArgs e)
        {
            //465; 453
            if (richTxtLogScreen.Visible)
            {
                richTxtLogScreen.Hide();
                this.MinimumSize = new Size(465, 460);
                this.MaximumSize = new Size(465, 860);
                this.Size = new Size(465, 460);
            }
            else
            {
                richTxtLogScreen.Show();
                this.Size = new Size(829, 460);
                this.MinimumSize = new Size(829, 460);
            }
        }

        private void ChckBoxEnableServerLogs_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.EnableServerLogs = chckBoxEnableServerLogs.Checked;
            Settings.Default.Save();
        }

        private void CmbSelectLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbSelectLanguage.SelectedValue == null)
            {
                return;
            }
            ComboBox cmb = (ComboBox)sender;
            try
            {
                string selectedValue = (string)cmb.SelectedValue;
                if (selectedValue != Settings.Default.ApplicationLanguage)
                {
                    Settings.Default.ApplicationLanguage = selectedValue;
                    Settings.Default.Save();
                    Application.Restart();
                    Environment.Exit(0);
                }
            }
            catch (Exception)
            {
            }
        }

        private Task AddData(string data)
        {
            DataRow row = dt.NewRow();
            row["resource"] = data;
            dt.Rows.Add(row);
            dataGridView1.DataSource = dt;
            return Task.CompletedTask;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            try
            {
                ((DataTable)dataGridView2.DataSource).DefaultView.RowFilter = string.Format("name like '%{0}%'", txtPlayerSearch.Text.Trim().Replace("'", "''"));
            }
            catch (Exception)
            {
            }
        }

        private void serveripsave()
        {            
            if (string.IsNullOrWhiteSpace(Settings.Default.ServerIP))
            {
                string serverip;
                serverip = Microsoft.VisualBasic.Interaction.InputBox("Please write server ip and port", "For example : http://localhost:30120 ", "");
                if (serverip.Contains(":"))
                {
                    Settings.Default.ServerIP = serverip;
                    Settings.Default.Save();
                    txtServerIp.Text = Settings.Default.ServerIP;
                }
                else
                {
                    MessageBox.Show("Please write server ip and port For example : http://localhost:30120");
                    serveripsave();
                }

            }
            else
            {
                txtServerIp.Text = Settings.Default.ServerIP;
            }
        }

        private void kickToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string reason;
            reason = Microsoft.VisualBasic.Interaction.InputBox("Please write reason", "Kick Reason", "");
            if (reason.Length > 5)
            {
                Ress("clientkick", dataGridView2.SelectedRows[0].Cells[1].Value.ToString(), reason);
            }
        }

        private void banToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            allKick();
        }

        private void allKick()
        {
            DialogResult allKick = new DialogResult();
            allKick = MessageBox.Show("Kick all users?", "Warning", MessageBoxButtons.YesNo);

            if (allKick == DialogResult.Yes)
            {
                count = 11;
                for (int i = 0; i < dataGridView2.RowCount; i++)
                {
                    Ress("clientkick", dataGridView2.Rows[i].Cells[1].Value.ToString(), "Sunucu Bakimda");
                }
                count = 0;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Settings.Default.ServerIP = txtServerIp.Text;
            Settings.Default.Save(); 
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (oneSyncCheck.Checked)
            {
                Settings.Default.OneSyncCheck = true;
                argument = "+set citizen_dir " + Settings.Default.ServerExeLocation.Replace("FXServer.exe", "") + "/citizen/ +exec server.cfg +set onesync_enabled 1";
            }
            else
            {
                Settings.Default.OneSyncCheck = false;
                argument = "+set citizen_dir " + Settings.Default.ServerExeLocation.Replace("FXServer.exe", "") +"/citizen/ +exec server.cfg";
            }
            Settings.Default.Save();
        }

        private Task addPlayer(string data, string id, string ping)
        {
            DataRow row = dt2.NewRow();
            row["name"] = data;
            row["id"] = id;
            row["ping"] = ping;
            dt2.Rows.Add(row);
            dataGridView2.DataSource = dt2;
            return Task.CompletedTask;
        }

        private Task serverInfo()
        {
            try
            {
                dt2.Clear();

            }
            catch (Exception)
            {
            }
            using (WebClient wc = new WebClient())
            {
                string name, id, ping;
                try
                {
                    var json = wc.DownloadString(txtServerIp.Text + "/players.json");
                    JArray a = JArray.Parse(json);
                    for (int i = 0; i < a.Count; i++)
                    {
                        name = a[i]["name"].ToString();
                        id = a[i]["id"].ToString();
                        ping = a[i]["ping"].ToString();
                        Thread addPlayerDatathread = new Thread(() =>
                        addPlayer(name, id, ping));
                        addPlayerDatathread.Start();
                    }
                }
                catch (Exception)
                {

                }

            }
            //http://35.204.151.166:30120/info.json
            //http://35.204.151.166:30120/players.json

            return Task.CompletedTask;
        }


    }
}
