﻿namespace LuteBot
{
    partial class PartitionsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.listBoxPartitions = new System.Windows.Forms.ListBox();
            this.button2 = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // listBoxPartitions
            // 
            this.listBoxPartitions.AllowDrop = true;
            this.listBoxPartitions.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxPartitions.FormattingEnabled = true;
            this.listBoxPartitions.ItemHeight = 16;
            this.listBoxPartitions.Location = new System.Drawing.Point(12, 12);
            this.listBoxPartitions.Name = "listBoxPartitions";
            this.listBoxPartitions.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBoxPartitions.Size = new System.Drawing.Size(497, 500);
            this.listBoxPartitions.TabIndex = 0;
            this.listBoxPartitions.DragDrop += new System.Windows.Forms.DragEventHandler(this.PartitionIndexBox_DragDrop);
            this.listBoxPartitions.DragOver += new System.Windows.Forms.DragEventHandler(this.PartitionIndexBox_DragOver);
            this.listBoxPartitions.MouseDown += new System.Windows.Forms.MouseEventHandler(this.PartitionIndexBox_MouseDown);
            // 
            // button2
            // 
            this.button2.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.button2.Location = new System.Drawing.Point(12, 516);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(155, 26);
            this.button2.TabIndex = 2;
            this.button2.Text = "Save Current Song";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button1
            // 
            this.button1.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.button1.Location = new System.Drawing.Point(187, 518);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(152, 23);
            this.button1.TabIndex = 3;
            this.button1.Text = "Import Partitions";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button3
            // 
            this.button3.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.button3.Location = new System.Drawing.Point(360, 518);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(152, 23);
            this.button3.TabIndex = 4;
            this.button3.Text = "Open Sav Folder";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // PartitionsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(524, 550);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.listBoxPartitions);
            this.Name = "PartitionsForm";
            this.Text = "LuteMod Partitions";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PartitionsForm_FormClosing);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox listBoxPartitions;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button3;
    }
}