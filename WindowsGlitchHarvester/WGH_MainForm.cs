﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WindowsGlitchHarvester
{
    public partial class WGH_MainForm : Form
    {

        public bool DontLoadSelectedStash = false;
        public bool DontLoadSelectedStockpile = false;

        public WGH_MainForm()
        {
            InitializeComponent();

            //Terrible ugly hack for the RefreshingListBox. Forgive my sins.
            //---------------------------------------------------------------
            var newListBox = new RefreshingListBox();
            newListBox.Anchor = lbStockpile.Anchor;
            newListBox.BackColor = lbStockpile.BackColor;
            newListBox.BorderStyle = lbStockpile.BorderStyle;
            newListBox.Font = lbStockpile.Font;
            newListBox.ForeColor = lbStockpile.ForeColor;
            newListBox.FormattingEnabled = lbStockpile.FormattingEnabled;
            newListBox.IntegralHeight = lbStockpile.IntegralHeight;
            newListBox.ItemHeight = lbStockpile.ItemHeight;
            newListBox.Location = lbStockpile.Location;
            newListBox.Name = lbStockpile.Name;
            newListBox.ScrollAlwaysVisible = lbStockpile.ScrollAlwaysVisible;
            newListBox.Size = lbStockpile.Size;
            newListBox.TabIndex = lbStockpile.TabIndex;
            newListBox.TabStop = lbStockpile.TabStop;
            newListBox.Tag = lbStockpile.Tag;
            newListBox.SelectionMode = lbStockpile.SelectionMode;
            newListBox.SelectedIndexChanged += new System.EventHandler(this.lbStockpile_SelectedIndexChanged);
            newListBox.MouseDown += new System.Windows.Forms.MouseEventHandler(this.lbStockpile_MouseDown);
            Controls.Remove(lbStockpile);
            lbStockpile = newListBox;
            Controls.Add(lbStockpile);
            //---------------------------------------------------------------


            WGH_Core.Start(this);
        }

        public void RunProgressBar(string progressLabel, int maxProgress, Action<object> action, Action<object> postAction = null)
        {

            if (WGH_Core.progressForm != null)
            {
                WGH_Core.progressForm.Close();
                this.Controls.Remove(WGH_Core.progressForm);
                WGH_Core.progressForm = null;
            }

            WGH_Core.progressForm = new WGH_Progress( progressLabel, maxProgress, action, postAction);
            WGH_Core.progressForm.Run();

        }


        private void btnBrowseTarget_Click(object sender, EventArgs e)
        {
            WGH_Core.LoadTarget();


                /*
                if (mi is ProcessInterface)
                {
                    cbTerminateOnReExec.Visible = false;
                    cbWriteCopyMode.Visible = false;
                    rbExecuteWith.Visible = false;
                    rbExecuteScript.Visible = false;
                    rbExecuteOtherProgram.Visible = false;
                    rbExecuteCorruptedFile.Visible = false;
                    rbNoExecution.Visible = false;
                    btnEditExec.Visible = false;
                    lbExecution.Visible = false;
                    cbInjectOnSelect.Visible = false;

                    WGH_Core.acForm.Visible = true;
                }
                else
                {
                */
                    cbTerminateOnReExec.Visible = true;
                    cbWriteCopyMode.Visible = true;
                    rbExecuteWith.Visible = true;
                    rbExecuteOtherProgram.Visible = true;
                    rbExecuteCorruptedFile.Visible = true;
                    rbNoExecution.Visible = true;
                    btnEditExec.Visible = true;
                    lbExecution.Visible = true;
                    cbInjectOnSelect.Visible = true;

                //}
            lbStashHistory.Items.Clear();
            lbStockpile.Items.Clear();

        }

        private void lbTargetName_Click(object sender, EventArgs e)
        {

        }

        private void btnBlastTarget_Click(object sender, EventArgs e)
        {
            WrapBlastTarget();

        }
        public void WrapBlastTarget(int times = 1, bool untilFound = false, bool stashBlastLayer = true)
        {
            if (WGH_Core.currentMemoryInterface == null)
                return;
            int intensity = WGH_Core.Intensity;

            bool multithread = WGH_Core.currentMemoryInterface.cacheEnabled;
            var cpus = Environment.ProcessorCount;

            Action<object> action = (ob) => {
                BlastTarget(times, untilFound, stashBlastLayer);
            };

            RunProgressBar($"Generating {String.Format("{0:#,##0}", intensity*times)} units {(multithread ? $"on {cpus} threads" : $"on 1 thread")} ...", intensity, action);

        }

        private volatile bool timeout = false;
        public void BlastTarget(int times = 1, bool untilFound = false, bool stashBlastLayer = true)
        {
            /*
            if (WGH_Core.currentMemoryInterface is ProcessInterface)
                (WGH_Core.currentMemoryInterface as ProcessInterface).RefreshSize();
            */
            

            System.Timers.Timer timeoutTimer = new System.Timers.Timer();
            timeoutTimer.Interval = 15000;
            timeoutTimer.Elapsed += delegate {
                timeout = true;
            };

            if (untilFound)
                timeoutTimer.Start();

            if (WGH_Core.currentMemoryInterface == null)
            {
                MessageBox.Show("No target is loaded");
                return;
            }

            TerminateIfNeeded();

            if (WGH_Core.currentMemoryInterface == null)
                return;

            WGH_Core.progressForm?.bw?.ReportProgress(0, "Uncorrupting");
            WGH_Core.RestoreTarget();

            for (int i = 0; i < times; i++)
            {
                if (untilFound && timeout)
                {
                    timeoutTimer.Stop();
                    MessageBox.Show(@"No effect found after 15 seconds, cancelling.");
                    break;
                }

                var bl = WGH_Core.Blast();

                if (bl != null)
                {
                    WGH_Core.currentStashkey = new StashKey(WGH_Core.GetRandomKey(), bl);

                    if (stashBlastLayer)
                    {
                        DontLoadSelectedStash = true;


                        WGH_Core.FormExecute((o) => {
                            lbStashHistory.Items.Add(WGH_Core.currentStashkey);
                            lbStashHistory.ClearSelected();
                            lbStashHistory.SelectedIndex = lbStashHistory.Items.Count - 1;
                            lbStockpile.ClearSelected();
                        });


                    }

                    if (untilFound)
                        break;
                }
            }
            WGH_Core.FormExecute((o) => { WGH_Executor.Execute(); });
        }

        private void TerminateIfNeeded()
        {
            if (rbExecuteOtherProgram.Checked || rbExecuteWith.Checked || rbExecuteCorruptedFile.Checked)
                if ( cbTerminateOnReExec.Checked && WGH_Executor.otherProgram != null)
                {
                    string otherProgramShortFilename = WGH_Executor.otherProgram.Substring(WGH_Executor.otherProgram.LastIndexOf(@"\") + 1);

                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = "taskkill";
                    startInfo.Arguments = $"/IM \"{otherProgramShortFilename}\"";
                    startInfo.RedirectStandardOutput = true;
                    startInfo.RedirectStandardError = true;
                    startInfo.UseShellExecute = false;
                    startInfo.CreateNoWindow = true;

                    Process processTemp = new Process();
                    processTemp.StartInfo = startInfo;
                    processTemp.EnableRaisingEvents = true;
                    try
                    {
                        processTemp.Start();
                        processTemp.WaitForExit();
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }

                    //Thread.Sleep(300);
                }
        }

        private void btnRestoreFileBackup_Click(object sender, EventArgs e)
        {
            if (WGH_Core.currentMemoryInterface != null)
                WGH_Core.currentMemoryInterface.RestoreBackup(true);

        }

        private void WGH_MainForm_Load(object sender, EventArgs e)
        {
            //new WGH_TestForm().ShowDialog();

            cbCorruptionEngine.SelectedIndex = 0;
            cbBlastType.SelectedIndex = 0;

            cbVectorLimiterList.SelectedIndex = 0;
            cbVectorValueList.SelectedIndex = 0;

            Controls.Remove(gbNightmareEngineSettings);
            Controls.Remove(gbVectorEngineSettings);

            pnCorruptionEngine.Controls.Add(gbNightmareEngineSettings);
            pnCorruptionEngine.Controls.Add(gbVectorEngineSettings);

            gbNightmareEngineSettings.Location = new Point(gbDefaultSettings.Location.X, gbDefaultSettings.Location.Y);
            gbVectorEngineSettings.Location = new Point(gbDefaultSettings.Location.X, gbDefaultSettings.Location.Y);

            this.Text = "WGH+ " + WGH_Core.WghVersion;

            mtb_Intensity.ValueChanged += Mtb_Intensity_ValueChanged;
            mtb_StartingAddress.ValueChanged += Mtb_StartingAddress_ValueChanged;

            mtb_BlastRange.ValueChanged += Mtb_BlastRange_ValueChanged;
            mtb_BlastRange.CheckChanged += Mtb_BlastRange_CheckChanged;

        }


        private void Mtb_BlastRange_CheckChanged(object sender, EventArgs e)
        {
            WGH_Core.useBlastRange = ((CheckBox)sender).Checked;
        }

        private void Mtb_BlastRange_ValueChanged(object sender, Components.ValueUpdateEventArgs e)
        {
            WGH_Core.BlastRange = Convert.ToInt32(mtb_BlastRange.Value);
        }
        private void Mtb_StartingAddress_ValueChanged(object sender, Components.ValueUpdateEventArgs e)
        {
            WGH_Core.StartingAddress = Convert.ToInt32(e.value);
        }

        private void Mtb_Intensity_ValueChanged(object sender, Components.ValueUpdateEventArgs e)
        {
            WGH_Core.Intensity = Convert.ToInt32(e.value);
        }

        private void cbCorruptionEngine_SelectedIndexChanged(object sender, EventArgs e)
        {

            gbNightmareEngineSettings.Visible = false;
            gbVectorEngineSettings.Visible = false;

            switch (cbCorruptionEngine.SelectedItem.ToString())
            {
                case "Nightmare Engine":
                    WGH_Core.selectedEngine = CorruptionEngine.NIGHTMARE;
                    gbNightmareEngineSettings.Visible = true;
                    break;

                case "Vector Engine":
                    WGH_Core.selectedEngine = CorruptionEngine.VECTOR;
                    gbVectorEngineSettings.Visible = true;
                    break;

            }
        }

        private void btnResetBackup_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(
@"This resets the backup of the current target by using the current data from it.
If you override a clean backup using a corrupted file,
you won't be able to restore the original file using it.

Are you sure you want to reset the current target's backup?", "WARNING", MessageBoxButtons.YesNo) == DialogResult.No)
                return;

            if (WGH_Core.currentMemoryInterface != null)
                WGH_Core.currentMemoryInterface.ResetBackup(true);

        }

        private void btnClearAllBackups_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to clear ALL THE BACKUPS\n from the Glitch Harvester's cache?", "WARNING", MessageBoxButtons.YesNo) == DialogResult.No)
                return;


            if (WGH_Core.currentMemoryInterface != null && WGH_Core.currentTargetType == "File")
                WGH_Core.currentMemoryInterface.RestoreBackup();

            foreach (string file in Directory.GetFiles(WGH_Core.currentDir + "\\TEMP"))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    MessageBox.Show($"Could not delete file {file}");
                }
            }

            WGH_Core.CompositeFilenameDico = new Dictionary<string, string>();
            if (WGH_Core.currentMemoryInterface != null && (WGH_Core.currentTargetType == "File" || WGH_Core.currentTargetType == "MultipleFiles"))
                WGH_Core.currentMemoryInterface.ResetBackup(false);
            WGH_Core.SaveCompositeFilenameDico();
            MessageBox.Show("All the backups were cleared.");
        }

        private void rbTargetFile_CheckedChanged(object sender, EventArgs e)
        {
            btnResetBackup.Enabled = true;
            btnRestoreFileBackup.Enabled = true;
            cbWriteCopyMode.Enabled = true;

            rbExecuteCorruptedFile.Enabled = true;
            rbExecuteWith.Enabled = true;
            rbExecuteOtherProgram.Enabled = true;
        }

        private void rbTargetMultipleFiles_CheckedChanged(object sender, EventArgs e)
        {
            btnResetBackup.Enabled = true;
            btnRestoreFileBackup.Enabled = true;
            cbWriteCopyMode.Enabled = true;

            if (rbExecuteCorruptedFile.Checked)
                rbNoExecution.Checked = true;
            rbExecuteCorruptedFile.Enabled = false;

            if (rbExecuteWith.Checked)
                rbNoExecution.Checked = true;

            rbExecuteWith.Enabled = false;
            rbExecuteOtherProgram.Enabled = true;
        }

        /*
        private void rbTargetProcess_CheckedChanged(object sender, EventArgs e)
        {
            btnResetBackup.Enabled = false;
            btnRestoreFileBackup.Enabled = false;
            cbWriteCopyMode.Enabled = false;

            rbExecuteCorruptedFile.Enabled = false;
            rbExecuteWith.Enabled = false;

            if (rbExecuteCorruptedFile.Checked || rbExecuteWith.Checked || rbExecuteOtherProgram.Checked)
                rbNoExecution.Checked = true;
        }
        */

        private void WGH_MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (WGH_Core.currentMemoryInterface != null)
                WGH_Core.RestoreTarget();
        }

        private void cbBlastType_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (cbBlastType.SelectedItem.ToString())
            {
                case "RANDOM":
                    WGH_NightmareEngine.Algo = BlastByteAlgo.RANDOM;
                    break;
                case "RANDOMTILT":
                    WGH_NightmareEngine.Algo = BlastByteAlgo.RANDOMTILT;
                    break;
                case "TILT":
                    WGH_NightmareEngine.Algo = BlastByteAlgo.TILT;
                    break;
            }
        }

        private void btnClearStockpile_Click(object sender, EventArgs e) => ClearStockpile();
        public void ClearStockpile(bool force = false)
        {
            if (force || MessageBox.Show("Are you sure you want to clear the stockpile?", "Clearing stockpile", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                lbStockpile.Items.Clear();

                //RTC_Restore.SaveRestore();
            }
        }

        private void cbWriteCopyMode_CheckedChanged(object sender, EventArgs e)
        {
            WGH_Core.writeCopyMode = cbWriteCopyMode.Checked;
        }

        private void btnClearStashHistory_Click(object sender, EventArgs e)
        {
            lbStashHistory.Items.Clear();
        }

        private void WrapInjectSelected()
        {

            TerminateIfNeeded();
            StashKey sk = null;


            WGH_Core.FormExecute((o) =>
            {
                if (lbStashHistory.SelectedIndex != -1)
                {
                    sk = (StashKey)lbStashHistory.SelectedItem;
                }
                else if (lbStockpile.SelectedIndex != -1)
                {
                    sk = (StashKey)lbStockpile.SelectedItem;
                }

            });

            if (sk != null)
            {
                WGH_Core.progressForm?.bw?.ReportProgress(1, "Uncorrupting...");
                WGH_Core.RestoreTarget();

                WGH_Core.progressForm?.bw?.ReportProgress(1, "Applying units...");
                sk.Run();
                WGH_Executor.Execute();
            }

        }

        private void btnInjectSelected_Click(object sender, EventArgs e)
        {
            if (WGH_Core.currentMemoryInterface == null)
            {
                MessageBox.Show("No target is loaded");
                return;
            }

            void action(object ob) => WrapInjectSelected();
            RunProgressBar($"Injecting units...", int.MaxValue, action);

        }

        private void btnEditExec_Click(object sender, EventArgs e)
        {
            WGH_Executor.EditExec();
        }

        private void rbNoExecution_CheckedChanged(object sender, EventArgs e)
        {
            WGH_Executor.RefreshLabel();
        }

        private void rbExecuteCorruptedFile_CheckedChanged(object sender, EventArgs e)
        {
            WGH_Executor.RefreshLabel();
        }

        private void rbExecuteOtherProgram_CheckedChanged(object sender, EventArgs e)
        {
            WGH_Executor.RefreshLabel();
        }

        private void rbExecuteScript_CheckedChanged(object sender, EventArgs e)
        {
            WGH_Executor.RefreshLabel();
        }

        private void btnAddStashToStockpile_Click(object sender, EventArgs e)
        {

            AddStashToStockpile();

            //RTC_Restore.SaveRestore();
        }

        public void AddStashToStockpile()
        {
            if (lbStashHistory.Items.Count == 0 || lbStashHistory.SelectedIndex == -1)
            {
                MessageBox.Show("Can't add the Stash to the Stockpile because none is selected in the Stash History");
                return;
            }

            string Name = "";
            string value = "";

            if (this.InputBox("Harvester", "Enter the new Stash name:", ref value) == DialogResult.OK)
            {
                Name = value.Trim();
            }
            else
            {
                return;
            }


            if (!String.IsNullOrEmpty(Name))
                WGH_Core.currentStashkey.Alias = Name;
            else
                WGH_Core.currentStashkey.Alias = WGH_Core.currentStashkey.Key;

            lbStockpile.Items.Add(WGH_Core.currentStashkey);
            lbStashHistory.Items.RemoveAt(lbStashHistory.SelectedIndex);

            DontLoadSelectedStockpile = true;
            lbStockpile.SelectedIndex = lbStockpile.Items.Count - 1;

        }

        public DialogResult InputBox(string title, string promptText, ref string value)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;

            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(9, 20, 372, 13);
            textBox.SetBounds(12, 36, 372, 20);
            buttonOk.SetBounds(228, 72, 75, 23);
            buttonCancel.SetBounds(309, 72, 75, 23);

            label.AutoSize = true;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 107);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
            form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            value = textBox.Text;
            return dialogResult;
        }


        private void lbStashHistory_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (DontLoadSelectedStash || lbStashHistory.SelectedIndex == -1)
            {
                DontLoadSelectedStash = false;
                return;
            }


            lbStockpile.ClearSelected();

            WGH_Core.currentStashkey = (lbStashHistory.SelectedItem as StashKey);

            if (cbInjectOnSelect.Checked)
                btnInjectSelected_Click(sender, e);
        }

        private void lbStockpile_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (DontLoadSelectedStockpile || lbStockpile.SelectedIndex == -1)
            {
                DontLoadSelectedStockpile = false;
                return;
            }

            lbStashHistory.ClearSelected();

            WGH_Core.currentStashkey = (lbStockpile.SelectedItem as StashKey);

            if (cbInjectOnSelect.Checked)
                btnInjectSelected_Click(sender, e);
        }

        private void btnRemoveSelected_Click(object sender, EventArgs e) => RemoveSelected();
        public void RemoveSelected(bool force = false)
        {
            if (lbStockpile.SelectedIndex != -1)
                if (force || MessageBox.Show($"Are you sure you want to remove item \"{lbStockpile.SelectedItem.ToString()}\" from Stockpile?", "Remove Item", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    lbStockpile.Items.RemoveAt(lbStockpile.SelectedIndex);
                    //RTC_Restore.SaveRestore();
                }
        }

        private void btnLoadStockpile_Click(object sender, EventArgs e)
        {
            if (WGH_Core.currentMemoryInterface == null)
            {
                MessageBox.Show("No target is loaded");
                return;
            }

            Stockpile.Load();
        }

        private void btnSaveStockpileAs_Click(object sender, EventArgs e)
        {
            if (lbStockpile.Items.Count == 0)
            {
                MessageBox.Show("You cannot save the Stockpile because it is empty");
                return;
            }

            Stockpile sks = new Stockpile(lbStockpile);
            Stockpile.Save(sks);
        }

        private void btnSaveStockpile_Click(object sender, EventArgs e)
        {
            Stockpile sks = new Stockpile(lbStockpile);
            Stockpile.Save(sks, true);
        }


        public void btnEnableCaching_Click(object sender, EventArgs e)
        {
            if (WGH_Core.currentMemoryInterface != null)
                if (btnEnableCaching.Text == "Enable Cache + Multithread")
                {
                    WGH_Core.currentMemoryInterface.getMemoryDump();

                    /*
                    if (WGH_Core.currentMemoryInterface is ProcessInterface)
                        (WGH_Core.currentMemoryInterface as ProcessInterface).UseCaching = true;
                    */

                    btnEnableCaching.Text = "Disable Cache + Multithread";
                }
                else
                {
                    WGH_Core.currentMemoryInterface.wipeMemoryDump();

                    /*
                    if (WGH_Core.currentMemoryInterface is ProcessInterface)
                        (WGH_Core.currentMemoryInterface as ProcessInterface).UseCaching = false;
                    */

                    btnEnableCaching.Text = "Enable Cache + Multithread";
                }
        }

        private void btnDisableAutoUncorrupt_Click(object sender, EventArgs e)
        {
                if (btnDisableAutoUncorrupt.Text == "Enable Auto-Uncorrupt")
                {
                    WGH_Core.AutoUncorrupt = true;
                    btnDisableAutoUncorrupt.Text = "Disable Auto-Uncorrupt";
                }
                else
                {
                    WGH_Core.AutoUncorrupt = false;
                    btnDisableAutoUncorrupt.Text = "Enable Auto-Uncorrupt";
                }
        }


        public static byte[] StringToByteArray(string hex)
        {
            byte[] arr = new byte[hex.Length / 2];

            for (int i = 0; i < hex.Length; i++)
                if (i % 2 == 0)
                    arr[i / 2] = Convert.ToByte(hex.Substring(i,2));

            return arr;
            /*
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
            */
        }

        private void btnStashHistoryUp_Click(object sender, EventArgs e)
        {
            if (lbStashHistory.SelectedIndex == -1)
                return;

            if (lbStashHistory.SelectedIndex == lbStashHistory.Items.Count - 1)
            {
                lbStashHistory.ClearSelected();
                lbStashHistory.SelectedIndex = 0;
            }

            else
            {
                int SelectedIndex = lbStashHistory.SelectedIndex;
                lbStashHistory.ClearSelected();
                lbStashHistory.SelectedIndex = SelectedIndex - 1;
            }

            //RTC_Restore.SaveRestore();
        }

        private void btnStashHistoryDown_Click(object sender, EventArgs e)
        {
            if (lbStashHistory.SelectedIndex == -1)
                return;

            if (lbStashHistory.SelectedIndex == lbStashHistory.Items.Count - 1)
            {
                lbStashHistory.ClearSelected();
                lbStashHistory.SelectedIndex = 0;
            }
            else
            {
                int SelectedIndex = lbStashHistory.SelectedIndex;
                lbStashHistory.ClearSelected();
                lbStashHistory.SelectedIndex = SelectedIndex + 1;
            }

            //RTC_Restore.SaveRestore();
        }

        private void btnStockpileUp_Click(object sender, EventArgs e)
        {
            if (lbStockpile.SelectedIndex == -1)
                return;

            if (lbStockpile.SelectedIndex == 0)
            {
                lbStockpile.ClearSelected();
                lbStockpile.SelectedIndex = lbStockpile.Items.Count - 1;
            }

            else
            {
                int SelectedIndex = lbStockpile.SelectedIndex;
                lbStockpile.ClearSelected();
                lbStockpile.SelectedIndex = SelectedIndex - 1;
            }

            //RTC_Restore.SaveRestore();
        }

        private void btnStockpileDown_Click(object sender, EventArgs e)
        {
            if (lbStockpile.SelectedIndex == -1)
                return;

            if (lbStockpile.SelectedIndex == lbStockpile.Items.Count - 1)
            {
                lbStockpile.ClearSelected();
                lbStockpile.SelectedIndex = 0;
            }

            else
            {

                int SelectedIndex = lbStockpile.SelectedIndex;
                lbStockpile.ClearSelected();
                lbStockpile.SelectedIndex = SelectedIndex + 1;
            }

            //RTC_Restore.SaveRestore();
        }

        private void btnStockpileMoveDown_Click(object sender, EventArgs e)
        {
            if (lbStockpile.Items.Count < 2)
                return;

            object o = lbStockpile.SelectedItem;
            int pos = lbStockpile.SelectedIndex;
            int count = lbStockpile.Items.Count;
            lbStockpile.Items.RemoveAt(pos);

            DontLoadSelectedStockpile = true;

            if (pos == count - 1)
            {
                lbStockpile.Items.Insert(0, o);
                lbStockpile.SelectedIndex = 0;
            }
            else
            {
                lbStockpile.Items.Insert(pos + 1, o);
                lbStockpile.SelectedIndex = pos + 1;
            }

            //RTC_Restore.SaveRestore();
        }

        private void btnStockpileMoveUp_Click(object sender, EventArgs e)
        {

            if (lbStockpile.Items.Count < 2)
                return;

            object o = lbStockpile.SelectedItem;
            int pos = lbStockpile.SelectedIndex;
            int count = lbStockpile.Items.Count;
            lbStockpile.Items.RemoveAt(pos);

            DontLoadSelectedStockpile = true;


            if (pos == 0)
            {
                lbStockpile.Items.Add(o);
                lbStockpile.SelectedIndex = count - 1;
            }
            else
            {
                lbStockpile.Items.Insert(pos - 1, o);
                lbStockpile.SelectedIndex = pos - 1;
            }

            //RTC_Restore.SaveRestore();

        }

        private void btnRenameSelected_Click(object sender, EventArgs e)
        {
            if (lbStockpile.SelectedIndex != -1)
            {

                string Name = "";
                string value = "";

                if (this.InputBox("Harvester", "Enter the new Stash name:", ref value) == DialogResult.OK)
                {
                    Name = value.Trim();
                }
                else
                {
                    return;
                }

                (lbStockpile.SelectedItem as StashKey).Alias = Name;
                (lbStockpile as RefreshingListBox).RefreshItemsReal();

            }

        }

        private void btnImportStockpile_Click(object sender, EventArgs e)
        {
            if (WGH_Core.currentMemoryInterface == null)
            {
                MessageBox.Show("No target is loaded");
                return;
            }

            Stockpile.Import();

            //RTC_Restore.SaveRestore();
        }

        private void RerollInject()
        {

            TerminateIfNeeded();

            StashKey sk = null;


            WGH_Core.FormExecute((o) => {
                if (lbStashHistory.SelectedIndex != -1)
                {
                    sk = (StashKey)lbStashHistory.SelectedItem;
                }
                else if (lbStockpile.SelectedIndex != -1)
                {
                    sk = (StashKey)lbStockpile.SelectedItem;
                }
            });

            if (sk != null)
            {
                WGH_Core.progressForm?.bw?.ReportProgress(0, "Cloning Stashkey");
                StashKey newSk = (StashKey)sk.Clone();
                newSk.Key = WGH_Core.GetRandomKey();
                newSk.Alias = null;

                WGH_Core.FormExecute((o) => {
                    WGH_Core.ghForm.DontLoadSelectedStash = true;
                    WGH_Core.ghForm.lbStashHistory.Items.Add(newSk);
                    WGH_Core.ghForm.lbStashHistory.ClearSelected();
                    WGH_Core.ghForm.lbStashHistory.SelectedIndex = WGH_Core.ghForm.lbStashHistory.Items.Count - 1;
                    WGH_Core.ghForm.lbStockpile.ClearSelected();
                });

                //TODO: Refactor this properly instead of as a hacky mess

                int reportCounter = 0;
                foreach (BlastUnit bu in newSk.BlastLayer.Layer)
                {
                    reportCounter++;
                    if (reportCounter > 1000)
                    {
                        WGH_Core.progressForm?.bw?.ReportProgress(1, "DEFAULT");
                        reportCounter = 0;
                    }



                    var bb = (bu as BlastByte);
                    var bv = (bu as BlastVector);
                    //BAD HACK. USE THIS TO DECTECT IF VECTOR. SORRY I DIDN'T KNOW WHERE TO REFACTOR THIS -NARRY
                    if (bb != null)
                    {
                        if (bb.Type == BlastByteType.SET)
                        {
                            bb.Value = WGH_Core.RND.Next(0, 255);
                        }
                        else if (bb.Type == BlastByteType.ADD || bb.Type == BlastByteType.SUBSTRACT)
                        {
                            int result = WGH_Core.RND.Next(1, 3);
                            switch (result)
                            {
                                case 1:
                                    bb.Type = BlastByteType.ADD;
                                    break;

                                case 2:
                                    bb.Type = BlastByteType.SUBSTRACT;
                                    break;

                            }
                        }
                    }
                    if (bv != null)
                    {
                        bv.Values = WGH_VectorEngine.getRandomConstant(WGH_VectorEngine.valueList);
                    }
                }

                WGH_Core.progressForm?.bw?.ReportProgress(1, "Uncorrupting...");
                WGH_Core.RestoreTarget();

                WGH_Core.progressForm?.bw?.ReportProgress(1, "Applying units...");
                newSk.Run();
                WGH_Executor.Execute();
            }
        }

        private void btnRerollInject_Click(object sender, EventArgs e)
        {

            if (WGH_Core.currentMemoryInterface == null)
            {
                MessageBox.Show("No target is loaded");
                return;
            }


            StashKey sk = null;

            if (lbStashHistory.SelectedIndex != -1)
                sk = (StashKey)lbStashHistory.SelectedItem;
            else if (lbStockpile.SelectedIndex != -1)
                sk = (StashKey)lbStockpile.SelectedItem;

            if (sk != null)
            {

                int intensity = sk.BlastLayer.Layer.Count;

                Action<object> action = (ob) => {
                    RerollInject();
                };

                RunProgressBar($"Rerolling {intensity} units...", intensity, action);

            }
            else
            {
                MessageBox.Show("Cannot reroll, no item is currently selected");
            }

        }

        private void cbVectorLimiterList_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedText = (sender as ComboBox).SelectedItem.ToString();

            switch (selectedText)
            {
                case "Extended":
                    WGH_VectorEngine.limiterList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.extendedListOfConstants);
                    break;
                case "Extended+":
                    WGH_VectorEngine.limiterList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.listOfPositiveConstants);
                    break;
                case "Extended-":
                    WGH_VectorEngine.limiterList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.listOfNegativeConstants);
                    break;
                case "SuperExtended":
                    WGH_VectorEngine.limiterList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.superExtendedListOfConstants);
                    break;
                case "Whole":
                    WGH_VectorEngine.limiterList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.listOfWholeConstants);
                    break;
                case "Whole+":
                    WGH_VectorEngine.limiterList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.listOfWholePositiveConstants);
                    break;
                case "Tiny":
                    WGH_VectorEngine.limiterList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.listOfTinyConstants);
                    break;
                case "One":
                    WGH_VectorEngine.limiterList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.constantPositiveOne);
                    break;
                case "One*":
                    WGH_VectorEngine.limiterList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.constantOne);
                    break;
                case "Two":
                    WGH_VectorEngine.limiterList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.constantPositiveTwo);
                    break;
                case "Custom":
                    WGH_VectorEngine.limiterList = WGH_VectorEngine.customList;
                    break;
                case "AnyFloat":
                    WGH_VectorEngine.limiterList = null;
                    break;


            }
        }

        private void cbVectorValueList_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedText = (sender as ComboBox).SelectedItem.ToString();

            switch (selectedText)
            {
                case "Extended":
                    WGH_VectorEngine.valueList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.extendedListOfConstants);
                    break;
                case "Extended+":
                    WGH_VectorEngine.valueList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.listOfPositiveConstants);
                    break;
                case "Extended-":
                    WGH_VectorEngine.valueList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.listOfNegativeConstants);
                    break;
                case "SuperExtended":
                    WGH_VectorEngine.valueList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.superExtendedListOfConstants);
                    break;
                case "Whole":
                    WGH_VectorEngine.valueList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.listOfWholeConstants);
                    break;
                case "Whole+":
                    WGH_VectorEngine.valueList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.listOfWholePositiveConstants);
                    break;
                case "Tiny":
                    WGH_VectorEngine.valueList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.listOfTinyConstants);
                    break;
                case "One":
                    WGH_VectorEngine.valueList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.constantPositiveOne);
                    break;
                case "One*":
                    WGH_VectorEngine.valueList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.constantOne);
                    break;
                case "Two":
                    WGH_VectorEngine.valueList = WGH_VectorEngine.stringListToIntList(WGH_VectorEngine.constantPositiveTwo);
                    break;
                case "Custom":
                    WGH_VectorEngine.valueList = WGH_VectorEngine.customList;
                    break;
                case "AnyFloat":
                    WGH_VectorEngine.valueList = null;
                    break;
            }
        }

        private void btnBlastTarget_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Point locate = new Point((sender as Control).Location.X + e.Location.X + pnBottom.Location.X, (sender as Control).Location.Y + e.Location.Y + pnBottom.Location.Y);

                ContextMenuStrip columnsMenu = new ContextMenuStrip();
                columnsMenu.Items.Add("Run 3 times", null, new EventHandler((ob, ev) => { WrapBlastTarget(5); }));
                columnsMenu.Items.Add("Run 10 times", null, new EventHandler((ob, ev) => { WrapBlastTarget(10); }));
                columnsMenu.Items.Add("Run 20 times", null, new EventHandler((ob, ev) => { WrapBlastTarget(20); }));
                columnsMenu.Items.Add("Run 30 times", null, new EventHandler((ob, ev) => { WrapBlastTarget(30); }));
                columnsMenu.Items.Add("Run 50 times", null, new EventHandler((ob, ev) => { WrapBlastTarget(50); }));
                columnsMenu.Items.Add("Run 75 times", null, new EventHandler((ob, ev) => { WrapBlastTarget(75); }));
                columnsMenu.Items.Add("Run 100 times", null, new EventHandler((ob, ev) => { WrapBlastTarget(100); }));
                columnsMenu.Items.Add("Run 1000 times", null, new EventHandler((ob, ev) => { WrapBlastTarget(1000); }));
                columnsMenu.Items.Add("Run 10000 times", null, new EventHandler((ob, ev) => { WrapBlastTarget(10000); }));
                columnsMenu.Show(this, locate);
            }

        }

        private void btnBlastUntilEffect_Click(object sender, EventArgs e)
        {
            Action<object> action = (ob) => {
                BlastTarget(int.MaxValue, true);
            };

            RunProgressBar($"Generating an undefined amount of units...\nThis can take a while...", int.MaxValue, action);
        }

        private void btnBlastUntilEffect_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Point locate = new Point((sender as Control).Location.X + e.Location.X + pnBottom.Location.X, (sender as Control).Location.Y + e.Location.Y + pnBottom.Location.Y);

                ContextMenuStrip columnsMenu = new ContextMenuStrip();
                columnsMenu.Items.Add("Run 3 times", null, new EventHandler((ob, ev) => { GuaranteedBlastTarget(5); }));
                columnsMenu.Items.Add("Run 10 times", null, new EventHandler((ob, ev) => { GuaranteedBlastTarget(10); }));
                columnsMenu.Items.Add("Run 20 times", null, new EventHandler((ob, ev) => { GuaranteedBlastTarget(20); }));
                columnsMenu.Items.Add("Run 30 times", null, new EventHandler((ob, ev) => { GuaranteedBlastTarget(30); }));
                columnsMenu.Items.Add("Run 50 times", null, new EventHandler((ob, ev) => { GuaranteedBlastTarget(50); }));
                columnsMenu.Items.Add("Run 75 times", null, new EventHandler((ob, ev) => { GuaranteedBlastTarget(75); }));
                columnsMenu.Items.Add("Run 100 times", null, new EventHandler((ob, ev) => { GuaranteedBlastTarget(100); }));
                columnsMenu.Show(this, locate);
            }
        }

        private void GuaranteedBlastTarget(int amount)
        {

            Action<object> action = (ob) => {
                for (int i = 0; i < amount; i++)
                    BlastTarget(int.MaxValue, true);
            };

            RunProgressBar($"Generating an undefined amount of units...\nThis can take a while...", int.MaxValue, action);

        }

        private void cbBigEndian_CheckedChanged(object sender, EventArgs e)
        {
            WGH_VectorEngine.BigEndian = cbBigEndian.Checked;
        }

        private void btnKillProcess_Click(object sender, EventArgs e)
        {
            TerminateIfNeeded();
        }

        private void lbStashHistory_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Point locate = new Point((sender as Control).Location.X + e.Location.X, (sender as Control).Location.Y + e.Location.Y);

                ContextMenuStrip columnsMenu = new ContextMenuStrip();
                (columnsMenu.Items.Add("Open Selected Item in Blast Editor", null, new EventHandler((ob, ev) => {
                    if (WGH_Core.beForm != null) WGH_Core.beForm.Close();
                    WGH_Core.beForm = new WGH_BlastEditorForm();
                    WGH_Core.beForm.LoadStashkey(WGH_Core.currentStashkey);
                })) as ToolStripMenuItem).Enabled = (lbStashHistory.SelectedIndex != -1 && lbStashHistory.SelectedItems.Count == 1);

                (columnsMenu.Items.Add("Merge Selected Stashkeys", null, new EventHandler((ob, ev) =>
                {
                    List<StashKey> sks = new List<StashKey>();
                    foreach (StashKey sk in lbStashHistory.SelectedItems)
                        sks.Add((StashKey)sk);

                    MergeStashkeys(sks);

                    RefreshStashHistory();
                })) as ToolStripMenuItem).Enabled = (lbStashHistory.SelectedIndex != -1 && lbStashHistory.SelectedItems.Count > 1);

                columnsMenu.Show(this, locate);
            }
        }

        private void lbStockpile_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Point locate = new Point((sender as Control).Location.X + e.Location.X, (sender as Control).Location.Y + e.Location.Y);

                ContextMenuStrip columnsMenu = new ContextMenuStrip();


                (columnsMenu.Items.Add("Open Selected Item in Blast Editor", null, new EventHandler((ob, ev) => {
                    if (WGH_Core.beForm != null)
                    {
                        WGH_Core.beForm.Close();
                        WGH_Core.beForm = new WGH_BlastEditorForm();
                        WGH_Core.beForm.LoadStashkey(WGH_Core.currentStashkey);
                    }
                    else
                    {
                        WGH_Core.beForm = new WGH_BlastEditorForm();
                        WGH_Core.beForm.LoadStashkey(WGH_Core.currentStashkey);
                    }

                })) as ToolStripMenuItem).Enabled = (lbStockpile.SelectedIndex != -1 && lbStockpile.SelectedItems.Count == 1);

                (columnsMenu.Items.Add("Merge Selected Stockpiles", null, new EventHandler((ob, ev) =>
                {
                    List<StashKey> sks = new List<StashKey>();
                    foreach (StashKey sk in lbStockpile.SelectedItems)
                        sks.Add((StashKey)sk);

                    MergeStashkeys(sks);

                    RefreshStashHistory();
                })) as ToolStripMenuItem).Enabled = (lbStockpile.SelectedIndex != -1 && lbStockpile.SelectedItems.Count > 1);


                columnsMenu.Show(this, locate);
            }
        }

        private void gbDefaultSettings_Enter(object sender, EventArgs e)
        {

        }


        private void vectorOffset_ValueChanged(object sender, EventArgs e)
        {
            WGH_VectorEngine.vectorOffset = Convert.ToInt64(vectorOffset.Value);
        }

        private void label13_Click(object sender, EventArgs e)
        {

        }

        private void cbVectorAligned_CheckedChanged(object sender, EventArgs e)
        {
            WGH_VectorEngine.vectorAligned = cbVectorAligned.Checked;
        }

        private void label10_Click(object sender, EventArgs e)
        {

        }

        private void savestateInfoButton_Click(object sender, EventArgs e)
        {

        }
        private void customWholeNumbers_CheckedChanged(object sender, EventArgs e)
        {
            WGH_VectorEngine.customWholeNumbers = customWholeNumbers.Checked;
        }

        private void valueMin_ValueChanged(object sender, EventArgs e)
        {
            WGH_VectorEngine.valueMin = Convert.ToInt32(valueMin.Value);
        }

        private void valueMax_ValueChanged(object sender, EventArgs e)
        {
            WGH_VectorEngine.valueMax = Convert.ToInt32(valueMax.Value);
        }
        
        private void limiterMin_ValueChanged(object sender, EventArgs e)
        {

            WGH_VectorEngine.limiterMin = Convert.ToInt32(limiterMin.Value);
        }

        private void limiterMax_ValueChanged(object sender, EventArgs e)
        {

            WGH_VectorEngine.limiterMax = Convert.ToInt32(limiterMax.Value);
        }

        public void RefreshStashHistory(bool scrolldown = false)
        {
            DontLoadSelectedStash = true;
            var lastSelect = lbStashHistory.SelectedIndex;


            if (lastSelect <= lbStashHistory.Items.Count)
            {
                lbStashHistory.ClearSelected();
                lbStashHistory.SelectedIndex = lastSelect;
            }
                

            DontLoadSelectedStash = false;

        }
        
		public static bool MergeStashkeys(List<StashKey> sks, bool _stashAfterOperation = true)
		{
			if (sks != null && sks.Count > 1)
            {
                BlastLayer bl = new BlastLayer();
				foreach (StashKey item in sks)
					bl.Layer.AddRange(item.BlastLayer.Layer);

                StashKey newSk = new StashKey(WGH_Core.GetRandomKey(), bl);
                WGH_Core.ghForm.lbStashHistory.Items.Add(newSk);

                WGH_Core.ghForm.RefreshStashHistory();

                WGH_Core.RestoreTarget();
                newSk.Run();
                WGH_Executor.Execute();

                return true;
			}
			else
			{
				MessageBox.Show("You need 2 or more items for Merging");
				return false;
			}

		}


        private void rbTargetDolphin_CheckedChanged(object sender, EventArgs e)
        {
            if (!File.Exists(WGH_Core.currentDir + "\\PARAMS\\DOLPHINWARNINGREAD") && File.Exists(WGH_Core.currentDir + "\\LICENSES\\DOLPHINWARNING.TXT"))
            {
                MessageBox.Show(File.ReadAllText(WGH_Core.currentDir + "\\LICENSES\\DOLPHINWARNING.TXT"), "WGH", MessageBoxButtons.OK, MessageBoxIcon.Information);
                File.Create(WGH_Core.currentDir + "\\PARAMS\\DOLPHINWARNINGREAD");
            }

            btnResetBackup.Enabled = true;
            btnRestoreFileBackup.Enabled = true;
            cbWriteCopyMode.Enabled = true;


            rbExecuteCorruptedFile.Enabled = false;
            rbExecuteWith.Enabled = false;
            rbExecuteOtherProgram.Enabled = false;

            rbNoExecution.Checked = true;

            cbVectorAligned.Checked = true;
            cbBigEndian.Checked = true;


        }

        private void buttonCBStash_Click(object sender, EventArgs e)
        {
            lbStashHistory.Items.Add(new StashKey(WGH_Core.GetRandomKey(), new BlastLayer()));
        }

        private void buttonImportBPS_Click(object sender, EventArgs e)
        {
            // Create stashkey
            StashKey sk = new StashKey(WGH_Core.GetRandomKey(), new BlastLayer());

            string origname = WGH_Core.currentTargetFullName;
            string patdname;

            // Ask for patched file
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select a patched file to import",
                CheckFileExists = true,
                Filter = "All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK) patdname = openFileDialog.FileName;
            else return;

            byte[] origbytes = File.ReadAllBytes(origname);
            byte[] patdbytes = File.ReadAllBytes(patdname);

            if (origbytes.Length != patdbytes.Length) MessageBox.Show(
                "The original and patched file are different lengths. Any bytes outside the range of the original file will be ignored.",
                $"WGH+ {WGH_Core.WghVersion}",
                MessageBoxButtons.OK
            );

            // pick the smallest length so we don't go outside the range of anything
            int patchlen = origbytes.Length > patdbytes.Length ? patdbytes.Length : origbytes.Length;

            for (int bytePointer = 0; bytePointer <= patchlen-1; bytePointer++)
            {
                if (origbytes[bytePointer] != patdbytes[bytePointer])
                {
                    sk.BlastLayer.Layer.Add(new BlastByte("File", bytePointer, BlastByteType.SET, patdbytes[bytePointer], true));
                }
            }

            WGH_Core.ghForm.DontLoadSelectedStash = true;
            WGH_Core.ghForm.lbStashHistory.Items.Add(sk);
            WGH_Core.ghForm.RefreshStashHistory();
            WGH_Core.ghForm.lbStockpile.ClearSelected();
            WGH_Core.ghForm.lbStashHistory.ClearSelected();
            WGH_Core.ghForm.lbStashHistory.SelectedIndex = WGH_Core.ghForm.lbStashHistory.Items.Count - 1;
        }
    }
}
