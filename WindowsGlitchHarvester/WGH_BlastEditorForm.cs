﻿using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WindowsGlitchHarvester
{
    public partial class WGH_BlastEditorForm : Form
    {
        StashKey sk = null;

        public WGH_BlastEditorForm()
        {
            InitializeComponent();
        }

        public void LoadStashkey(StashKey _sk)
        {
            if (_sk == null)
                return;

            //lbBlastLayer.Items.Clear();
            sk = (StashKey)_sk.Clone();

            lbBlastLayer.DataSource = sk.BlastLayer.Layer;

            //foreach (var item in sk.blastlayer.Layer)
            //	lbBlastLayer.Items.Add(item);
            RefreshBlastLayer();
            this.Show();
        }

        public void RefreshBlastLayer()
        {
            lbBlastLayer.DataSource = null;
            lbBlastLayer.DataSource = sk.BlastLayer.Layer;
            lbBlastLayerSize.Text = "BlastLayer size: " + sk.BlastLayer.Layer.Count.ToString();
        }

        private void btnDisable50_Click(object sender, EventArgs e)
        {
            foreach (BlastUnit bu in sk.BlastLayer.Layer)
                bu.IsEnabled = true;

            List<BlastUnit> unsortedList = new List<BlastUnit>(sk.BlastLayer.Layer.ToArray());
            List<BlastUnit> randomizedList = new List<BlastUnit>();

            while (unsortedList.Count>0)
            {
                int randomPick = WGH_Core.RND.Next(unsortedList.Count);
                BlastUnit bu = unsortedList[randomPick];
                unsortedList.RemoveAt(randomPick);
                randomizedList.Add(bu);
            }

            List<BlastUnit> halfList = randomizedList.GetRange(0, (randomizedList.Count / 2));

            foreach (BlastUnit bu in halfList)
                bu.IsEnabled = false;

            RefreshBlastLayer();
        }

        private void btnRemoveDisabled_Click(object sender, EventArgs e)
        {
            List<BlastUnit> newLayer = new List<BlastUnit>();

            foreach (BlastUnit bu in sk.BlastLayer.Layer)
                if (bu.IsEnabled)
                    newLayer.Add(bu);

            sk.BlastLayer.Layer = newLayer;

            RefreshBlastLayer();
        }

        private void btnLoadCorrupt_Click(object sender, EventArgs e)
        {
            BlastLayer bl = new BlastLayer();

            foreach (var item in lbBlastLayer.Items)
            {
                BlastUnit bu = (item as BlastUnit);
                if (bu.IsEnabled)
                    bl.Layer.Add(bu);
            }

            StashKey newSk = (StashKey)sk.Clone();

            newSk.BlastLayer = bl;
            WGH_Core.RestoreTarget();
            newSk.Run();
            WGH_Executor.Execute();
        }
        /*
        private void btnCorrupt_Click(object sender, EventArgs e)
        {
            BlastLayer bl = new BlastLayer();

            foreach (var item in lbBlastLayer.Items)
            {
                BlastUnit bu = (item as BlastUnit);
                if (bu.IsEnabled)
                    bl.Layer.Add(bu);
            }
            
            WGH_Core.RestoreTarget();
            bl.Apply();
        }*/

        private void btnSendToStash_Click(object sender, EventArgs e)
        {
            StashKey newSk = (StashKey)sk.Clone();
            newSk.Key = WGH_Core.GetRandomKey();
            newSk.Alias = null;

            WGH_Core.ghForm.DontLoadSelectedStash = true;
            WGH_Core.ghForm.lbStashHistory.Items.Add(newSk);
            WGH_Core.ghForm.RefreshStashHistory();
            //  WGH_Core.ghForm.lbStashHistory.Items.Add(WGH_Core.currentStashkey);
            WGH_Core.ghForm.lbStockpile.ClearSelected();
            WGH_Core.ghForm.lbStashHistory.ClearSelected();
            WGH_Core.ghForm.lbStashHistory.SelectedIndex = WGH_Core.ghForm.lbStashHistory.Items.Count - 1;

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private void lbBlastLayer_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (lbBlastLayer.SelectedIndex == -1)
                return;

            (lbBlastLayer.SelectedItem as BlastUnit).IsEnabled = !(lbBlastLayer.SelectedItem as BlastUnit).IsEnabled;

            RefreshBlastLayer();
        }

        private void btnInvertDisabled_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < sk.BlastLayer.Layer.Count; i++)
                sk.BlastLayer.Layer[i].IsEnabled = !sk.BlastLayer.Layer[i].IsEnabled;

            RefreshBlastLayer();
        }

        private void lbBlastLayer_SelectedIndexChanged(object sender, EventArgs e)
        {
            gbAddressEdit.Visible = false;
            gbValueEdit.Visible = false;

            lbAddressEdit.Text = "Address Edit:";
            lbValueEdit.Text = "Value Edit:";

            if (lbBlastLayer.SelectedIndex == -1)
                return;

            if (lbBlastLayer.SelectedItem is BlastByte)
            {
                gbAddressEdit.Visible = true;
                gbValueEdit.Visible = true;
                nmAddressEdit.Value = (lbBlastLayer.SelectedItem as BlastByte).Address;
                nmValueEdit.Value = (lbBlastLayer.SelectedItem as BlastByte).Value;
            }
            /*
            else if (lbBlastLayer.SelectedItem is BlastCheat)
            {
                gbAddressEdit.Visible = true;
                nmAddressEdit.Value = (lbBlastLayer.SelectedItem as BlastCheat).Address;
                if (!(lbBlastLayer.SelectedItem as BlastCheat).IsFreeze)
                {
                    gbValueEdit.Visible = true;
                    nmValueEdit.Value = (lbBlastLayer.SelectedItem as BlastCheat).Value;
                }
            }
            else if (lbBlastLayer.SelectedItem is BlastPipe)
            {
                lbAddressEdit.Text = "Address Edit:";
                lbValueEdit.Text = "PipeAddress Edit:";

                gbAddressEdit.Visible = true;
                gbValueEdit.Visible = true;
                nmAddressEdit.Value = (lbBlastLayer.SelectedItem as BlastPipe).Address;
                nmValueEdit.Value = (lbBlastLayer.SelectedItem as BlastPipe).PipeAddress;
            }
            */
        }

        private void btnAdressUpdate_Click(object sender, EventArgs e)
        {
            if (lbBlastLayer.SelectedItem is BlastByte)
                (lbBlastLayer.SelectedItem as BlastByte).Address = Convert.ToInt64(nmAddressEdit.Value);
            /*
            else if (lbBlastLayer.SelectedItem is BlastCheat)
                (lbBlastLayer.SelectedItem as BlastCheat).Address = Convert.ToInt64(nmAddressEdit.Value);
            else if (lbBlastLayer.SelectedItem is BlastPipe)
                (lbBlastLayer.SelectedItem as BlastPipe).Address = Convert.ToInt64(nmAddressEdit.Value);
            */

            RefreshBlastLayer();
        }

        private void btnValueUpdate_Click(object sender, EventArgs e)
        {
            if (lbBlastLayer.SelectedItem is BlastByte)
                (lbBlastLayer.SelectedItem as BlastByte).Value = Convert.ToInt32(nmValueEdit.Value);
            /*
            else if (lbBlastLayer.SelectedItem is BlastCheat)
                (lbBlastLayer.SelectedItem as BlastCheat).Value = Convert.ToInt32(nmValueEdit.Value);
            else if (lbBlastLayer.SelectedItem is BlastPipe)
                (lbBlastLayer.SelectedItem as BlastPipe).PipeAddress = Convert.ToInt64(nmValueEdit.Value);
            */

            RefreshBlastLayer();
        }

        private void btnDisableEverything_Click(object sender, EventArgs e)
        {
            foreach (BlastUnit bu in sk.BlastLayer.Layer)
                bu.IsEnabled = false;

            RefreshBlastLayer();
        }

        private void btnEnableEverything_Click(object sender, EventArgs e)
        {
            foreach (BlastUnit bu in sk.BlastLayer.Layer)
                bu.IsEnabled = true;

            RefreshBlastLayer();
        }

        public DialogResult getInputBox(string title, string promptText, ref string value)
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


        private void nmAddressEdit_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Point locate = new Point((sender as Control).Location.X + e.Location.X + gbAddressEdit.Location.X, (sender as Control).Location.Y + e.Location.Y + gbAddressEdit.Location.Y);

                ContextMenuStrip columnsMenu = new ContextMenuStrip();
                columnsMenu.Items.Add("Import from Hex", null, new EventHandler((ob, ev) =>
                {
                    string value = "";
                    if (this.getInputBox("Import from Hex", "Enter Hexadecimal number:", ref value) == DialogResult.OK)
                    {
                        int newValue = int.Parse(value, NumberStyles.HexNumber);
                        nmAddressEdit.Value = Convert.ToDecimal(newValue);
                    }
                }));
                columnsMenu.Show(this, locate);
            }
        }

        private void nmValueEdit_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Point locate = new Point((sender as Control).Location.X + e.Location.X + gbValueEdit.Location.X, (sender as Control).Location.Y + e.Location.Y + gbValueEdit.Location.Y);

                ContextMenuStrip columnsMenu = new ContextMenuStrip();
                columnsMenu.Items.Add("Import from Hex", null, new EventHandler((ob, ev) =>
                {
                    string value = "";
                    if (this.getInputBox("Import from Hex", "Enter Hexadecimal number:", ref value) == DialogResult.OK)
                    {
                        int newValue = int.Parse(value, NumberStyles.HexNumber);
                        nmValueEdit.Value = Convert.ToDecimal(newValue);
                    }
                }));
                columnsMenu.Show(this, locate);
            }
        }

        private void RTC_BlastEditorForm_Load(object sender, EventArgs e)
        {
            ContextMenu contextMenu = new ContextMenu();
            this.nmAddressEdit.ContextMenu = contextMenu;
            this.nmValueEdit.ContextMenu = contextMenu;
        }

        private void btnDuplicateSelected_Click(object sender, EventArgs e)
        {
            if (lbBlastLayer.SelectedIndex == -1)
            {
                MessageBox.Show("No unit was selected. Cannot duplicate.");
                return;
            }

            int pos = lbBlastLayer.SelectedIndex;

            BlastUnit bu = sk.BlastLayer.Layer[pos];
            BlastUnit bu2 = ObjectCopier.Clone(bu);
            sk.BlastLayer.Layer.Insert(pos, bu2);

            MessageBox.Show(((BlastByte)bu).Domain);

            RefreshBlastLayer();
        }

        private void btnSanitizeDuplicates_Click(object sender, EventArgs e)
        {
            var arr = sk.BlastLayer.Layer.ToArray();
            Array.Reverse(arr);
            List<BlastUnit> bul = new List<BlastUnit>(arr);
            List<long> usedAddresses = new List<long>();
            //This is bad but I'm not refactoring blastunit right now to do it cleaner -Narry
            foreach (BlastUnit bu in bul)
            {
                var bb = (bu as BlastByte);
                var bv = (bu as BlastVector);
                
                if(bb != null)
                {
                    if (!usedAddresses.Contains(bb.Address))
                        usedAddresses.Add(bb.Address);
                    else
                        sk.BlastLayer.Layer.Remove(bu);
                }
                else if (bv != null)
                {
                    if (!usedAddresses.Contains(bv.Address))
                        usedAddresses.Add(bv.Address);
                    else
                        sk.BlastLayer.Layer.Remove(bv);
                }
            }
            RefreshBlastLayer();

        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void btnImportBPS_Click(object sender, EventArgs e)
        {
            // Ask for BPS
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select a BPS patch file to import",
                CheckFileExists = true,
                Filter = "BPS Patch File (*.bps)|*.bps"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // Clear all changes
                sk.BlastLayer.Layer.Clear();
                RefreshBlastLayer();
                // Process .bps file
                Process proc = Process.Start(new ProcessStartInfo { 
                    FileName = @"pythonbps\py38\python.exe", 
                    Arguments = $"pythonbps\\test.py \"{openFileDialog.FileName}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
                while (!proc.StandardOutput.EndOfStream)
                {
                    string line = proc.StandardOutput.ReadLine();
                    Debug.WriteLine(line);
                    if (line.StartsWith("sourceread: "))
                    {
                        long address;
                        int value;

                        address = long.Parse(line.Split(' ')[1]);

                        proc.StandardOutput.ReadLine(); // Skip one line
                        line = proc.StandardOutput.ReadLine();
                        Debug.WriteLine(line);

                        if (line.StartsWith("targetcrc32: ")) break;

                        value = int.Parse( line, NumberStyles.HexNumber );

                        sk.BlastLayer.Layer.Add(new BlastByte("File", address, BlastByteType.SET, value, true));
                        RefreshBlastLayer();
                    }
                }
                RefreshBlastLayer();
            }
        }
    }
}
