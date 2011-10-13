using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Data;
using System.Drawing;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using System.Management;

// zCPFS20aGvm0nsl0l2h1V4cUUY33btqRXDXCeWwFtxQ

namespace LOLTW_Uploader
{
    public partial class Main : Form
    {
        private static FileSystemWatcher fsw;
        private string pathLOLTWLog = null;
        private DateTime lastTimeFileWatcherEventRaised;
        private DateTime lastUploaded;
        private List<FileInfo> fileWaitUpload;
        private System.Timers.Timer timer;

        public Main()
        {
            InitializeComponent();

            ShowBallon("台灣英雄聯盟戰績網戰績上傳器", "已經啟動。左鍵點兩下圖示或右鍵放大可打開主畫面。", 1);

            Initiate();
            this.Visible = false;
        }

        private void Initiate()
        {
            string pathLOLTW = LOLLib.GetLOLTWPath();
            if (pathLOLTW == null)
            {
                MessageBox.Show("程式找不到您的 LOL TW 安裝資料夾，什麼事都做不了，快把我關了！");
                return;
            }

            pathLOLTWLog = string.Format(@"{0}\Air\logs", pathLOLTW);
            lastTimeFileWatcherEventRaised = DateTime.Now;
            if (DateTime.TryParse(LOLLib.IniReadValue("upload", "last_upload"), out lastUploaded) == false)
            {
                lastUploaded = DateTime.Now;
                LOLLib.IniWriteValue("upload", "last_upload", string.Format("{0:s}", lastUploaded));
            }

            LOLLib.lastUploaded = lastUploaded;

            fileWaitUpload = LOLLib.ScanFolder(pathLOLTWLog);
            ShowStatusText(string.Format("共有 {0} 個 log 檔等候上傳。", fileWaitUpload.Count));

            this.checkBox_AutoRun.Checked = !(LOLLib.GetLOLAutoRun() == "False");

            bgUpload.WorkerReportsProgress = true; // 設定可報告進度
            bgUpload.WorkerSupportsCancellation = true; // 設定可中止
            bgUpload.DoWork += new DoWorkEventHandler(bgUpload_DoWork);
            bgUpload.ProgressChanged += new ProgressChangedEventHandler(bgUpload_ProgressChanged);
            bgUpload.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bgUpload_RunWorkerCompleted);

            // 一個小時跑一次
            timer = new System.Timers.Timer();
            timer.Interval = 60 * 60 * 1000;
            timer.Elapsed += new System.Timers.ElapsedEventHandler(timer_Elapsed);
            timer.Enabled = true;
        }

        #region gui function

        private delegate void UpdateUICallBack(Control ctl, string property, string value);
        private delegate void UpdateStatusTextCallBack(ToolStripStatusLabel ctl, string value);
        //跨執行緒更改label文字內容,需用委派方法
        private void UpdateUI(Control ctl, string property, string value)
        {
            if (this.InvokeRequired)
            {
                UpdateUICallBack uu = new UpdateUICallBack(UpdateUI);
                this.Invoke(uu, value, ctl);
                return;
            }
            ctl.GetType().GetProperty(property).SetValue(ctl, property, null);
        }

        private void UpdateStatusText(ToolStripStatusLabel ctl, string value)
        {
            if (this.InvokeRequired)
            {
                UpdateStatusTextCallBack uu = new UpdateStatusTextCallBack(UpdateStatusText);
                this.Invoke(uu, ctl, value);
                return;
            }
            ctl.Text = value;
        }

        private void ShowBallon(string title, string text, int time)
        {
            this.notifyIcon.ShowBalloonTip(time, title, text, ToolTipIcon.Info);
        }

        private void ShowStatusText(string msg)
        {
            UpdateStatusText(statusLabel, msg);
        }

        #endregion

        #region 處理檔案
        // 實際上沒用到… 用 watcher 來看他沒什麼意義…
        // 一場遊戲15～30分鐘，而且 log 在 client 結束才寫入…
        // 還不如用 timer 定期檢查就好

        private void ManageFolder()
        {
            fsw = new FileSystemWatcher(pathLOLTWLog, "*.log");
            fsw.NotifyFilter = NotifyFilters.LastWrite;
            fsw.Changed += new FileSystemEventHandler(fsw_Change);
            //fsw.Created += new FileSystemEventHandler(fsw_change);
            fsw.EnableRaisingEvents = true;
            // 壓縮
            // 上傳
        }


        // 檔案改變實際上會觸發兩次，所以小於 500 ms 的修改當作同一次
        // http://stackoverflow.com/questions/449993/vb-net-filesystemwatcher-multiple-change-events/450046#450046

        private void fsw_Change(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                if (DateTime.Now.Subtract(lastTimeFileWatcherEventRaised).TotalMilliseconds < 500)
                {
                    return;
                }
                lastTimeFileWatcherEventRaised = DateTime.Now;
            }
            //MessageBox.Show(string.Format("資料夾已({0}):{1}:{2}", e.ChangeType, e.FullPath, DateTime.Now.Subtract(lastTimeFileWatcherEventRaised).TotalMilliseconds));
            //LOLLib.logit(string.Format("資料夾已({0}):{1}:{2}", e.ChangeType, e.FullPath, DateTime.Now.Subtract(lastTimeFileWatcherEventRaised).TotalMilliseconds));
        }

        #endregion

        #region Event

        private void 結束XToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void 放大ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Visible = true;
            //this.ShowInTaskbar = true;
            this.WindowState = FormWindowState.Normal;
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            this.Visible = true;
            this.WindowState = FormWindowState.Normal;
            //this.ShowInTaskbar = true;
        }

        const int WM_SYSCOMMAND = 0x112;
        const int SC_CLOSE = 0xF060;
        const int SC_MINIMIZE = 0xF020;
        const int SC_MAXIMIZE = 0xF030;
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SYSCOMMAND)
            {
                if (m.WParam.ToInt32() == SC_MINIMIZE)
                {
                    this.Visible = false;
                    this.ShowInTaskbar = false;
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private void buttonUpload_Click(object sender, EventArgs e)
        {
            //WebClient wc = new WebClient();
            //byte[] responseArray = wc.UploadFile(urlUpload, "POST", @"D:\Games\GarenaLoLTW\GameData\Apps\LoLTW\Air\logs\LolClient.20110928.010541.log");
            //MessageBox.Show(System.Text.Encoding.ASCII.GetString(responseArray));
            if (bgUpload.IsBusy)
            {
                MessageBox.Show("log 檔上傳中～別急嘛～請稍後再按！");
            }
            else
            {
                bgUpload.RunWorkerAsync();
            }
        }

        void bgUpload_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //收到進度回報，更新畫面上的進度列
            ShowStatusText(string.Format("正在上傳第 {0} 個 log 檔…", e.ProgressPercentage.ToString()));
        }

        void bgUpload_DoWork(object sender, DoWorkEventArgs e)
        {
            //工作內容

            if (fileWaitUpload.Count <= 0)
            {
                ShowStatusText("已經沒有 log 需要上傳了。");
                return;
            }

            int i = 1;

            LOLLib.HandelFiles(fileWaitUpload);

            /*
            foreach (FileInfo fi in fileWaitUpload)
            {
                (sender as BackgroundWorker).ReportProgress(i);
                LOLLib.HandelFile(fi);
                i++;
            }
            */

            //偵測使用者是否中止
            if ((sender as BackgroundWorker).CancellationPending)
            {
                e.Cancel = true;
                return;
            }
        }

        void bgUpload_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //完成
            if (e.Error != null)
            {
                //以失敗收場
            }
            else if (e.Cancelled)
            {
                //工作被取消
            }
            else
            {
                ShowStatusText("log 全數上傳完畢了。");
            }
        }

        void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            fileWaitUpload = LOLLib.ScanFolder(pathLOLTWLog);
            ShowStatusText(string.Format("共有 {0} 個 log 檔等候上傳。", fileWaitUpload.Count));
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer.Enabled = false;
            timer.Dispose();
        }

        private void checkBox_AutoRun_CheckedChanged(object sender, EventArgs e)
        {
            LOLLib.CheckLOLAutoRun(((CheckBox)sender).Checked);

        }

        #endregion
    }
}
