﻿using LuteBot.Config;
using LuteBot.Core.Midi;
using LuteBot.IO.Files;
using LuteBot.TrackSelection;
using LuteBot.UI.Utils;

using LuteMod.Indexing;
using LuteMod.Sequencing;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LuteBot
{
    public partial class PartitionsForm : Form
    {
        public PartitionsForm(TrackSelectionManager tsm, MidiPlayer player)
        {
            this.tsm = tsm;
            this.player = player;
            InitializeComponent();
            RefreshPartitionList();
        }

        private MidiPlayer player;
        private PartitionIndex index;
        private TrackSelectionManager tsm;

        private void RefreshPartitionList()
        {
            index = new PartitionIndex();
            index.LoadIndex();
            if (!index.Loaded)
            {
                MessageBox.Show("No partition index found.  Initialize the partition by opening the partition menu in-game with lutemod at least once");
            }
            PopulateIndexList();
        }

        private void PopulateIndexList()
        {
            listBoxPartitions.Items.Clear();
            foreach (string item in index.PartitionNames)
            {
                listBoxPartitions.Items.Add(item);
            }
        }

        private void ContextMenuHelper()
        {
            if (listBoxPartitions.Items.Count > 0 && listBoxPartitions.SelectedIndex >= 0)
            {
                ContextMenu indexContextMenu = new ContextMenu();

                MenuItem deleteItem = indexContextMenu.MenuItems.Add("Delete");
                deleteItem.Click += new EventHandler(DeleteItem_Click);
                listBoxPartitions.ContextMenu = indexContextMenu;
                indexContextMenu.Show(listBoxPartitions, listBoxPartitions.PointToClient(Cursor.Position));
            }
            else
            {
                listBoxPartitions.ContextMenu = null;
            }
        }

        private void PartitionIndexBox_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void PartitionIndexBox_DragDrop(object sender, DragEventArgs e)
        {
            Point point = listBoxPartitions.PointToClient(new Point(e.X, e.Y));
            int i = this.listBoxPartitions.IndexFromPoint(point);
            if (i < 0) i = this.listBoxPartitions.Items.Count - 1;
            object data = e.Data.GetData(typeof(string));
            if (data != null)
            {
                this.listBoxPartitions.Items.Remove(data);
                this.listBoxPartitions.Items.Insert(i, data);
                index.PartitionNames.Remove((string)data);
                index.PartitionNames.Insert(i, (string)data);
                index.SaveIndex();
            }
        }

        private void PartitionIndexBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (this.listBoxPartitions.SelectedItem == null) return;
            if (Control.ModifierKeys == Keys.None) // Prevents a bug when multi-selecting
                this.listBoxPartitions.DoDragDrop(this.listBoxPartitions.SelectedItem, DragDropEffects.Move);
            if (e.Button == MouseButtons.Right)
            {
                ContextMenuHelper();
            }
        }

        private void DeleteItem_Click(object sender, EventArgs e)
        {
            DialogResult confirmResult = MessageBox.Show("Do you want to delete this partition ?",
                                     "Confirm Deletion",
                                     MessageBoxButtons.YesNo);
            if (confirmResult == DialogResult.Yes)
            {
                var selectedList = new List<int>();
                IEnumerable<int> selectedEnum;

                selectedEnum = listBoxPartitions.SelectedIndices.Cast<int>();
                // Just in case some weird shit happens again
                // TLDR, because MouseDown had DoDragDrop, it induced a rare bug in .net framework which made it fail to iterate or AddRange for this type of ListBox
                // We now only DoDragDrop if a key isn't held, so it should be relatively impossible to induce, but if it does, it will silently fail
                try
                {
                    selectedList.AddRange(selectedEnum);
                }
                catch { return; }
                selectedList.Sort((a, b) => b.CompareTo(a)); // Sort largest first so we don't have issues when we remove them
                foreach (int selected in selectedList)
                {
                    try
                    {
                        SaveManager.DeleteData(SaveManager.SaveFilePath + index.PartitionNames[selected]);
                        index.PartitionNames.RemoveAt(selected);
                    }
                    catch { }
                }
                PopulateIndexList();
                index.SaveIndex();
            }
        }

        private void ImportPartitionButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog openSavFileDialog = new OpenFileDialog();
            string[] fileNames;
            openSavFileDialog.DefaultExt = "sav";
            openSavFileDialog.Filter = "SAV files|*.sav";
            openSavFileDialog.Title = "Open SAV file";
            openSavFileDialog.Multiselect = true;
            if (openSavFileDialog.ShowDialog() == DialogResult.OK)
            {
                fileNames = openSavFileDialog.FileNames;
                foreach (string fileName in fileNames)
                {
                    index.AddFileInIndex(fileName);
                }
                index.SaveIndex();
                PopulateIndexList();
            }
        }


        private LuteMod.Converter.MordhauConverter trackConverter = null;

        private void button2_Click(object sender, EventArgs e)
        {

            if (tsm.MidiTracks.Where(t => t.Active).Count() > 0)
            {
                var namingForm = new TrackNamingForm(Path.GetFileNameWithoutExtension(tsm.FileName));
                namingForm.ShowDialog(this);
                if (namingForm.DialogResult == DialogResult.OK)
                {
                    if (namingForm.textBoxPartName.Text == "Partition Name" || namingForm.textBoxPartName.Text.Trim() == "")
                    {
                        MessageBox.Show("Please name your partition");
                    }
                    else
                    {
                        if (index.PartitionNames.Contains(namingForm.textBoxPartName.Text))
                        {
                            MessageBox.Show("That name already exists");
                        }
                        else
                        {
                            if (!Regex.IsMatch(namingForm.textBoxPartName.Text, "^([a-zA-Z0-9][a-zA-Z0-9 -]*[a-zA-Z0-9])$"))
                            {
                                MessageBox.Show("That name contains invalid characters");
                            }
                            else
                            {
                                index.PartitionNames.Add(namingForm.textBoxPartName.Text);

                                //if (trackConverter == null)
                                //{
                                var converter = new LuteMod.Converter.MordhauConverter();
                                int firstInstrument = ConfigManager.GetIntegerProperty(PropertyItem.Instrument);
                                // Step 1, load solo lute into track 0 - this profile should always exist
                                // Actually, all of the first 4 instruments get loaded in, under the same ID we use in lutebot.  Convenient.
                                for (int i = 0; i < 4; i++)
                                {
                                    int oldInstrument = ConfigManager.GetIntegerProperty(PropertyItem.Instrument);

                                    if (oldInstrument != i)
                                    {
                                        ConfigManager.SetProperty(PropertyItem.Instrument, i.ToString());
                                        Instrument target = Instrument.Prefabs[i];

                                        bool soundEffects = !target.Name.StartsWith("Mordhau", true, System.Globalization.CultureInfo.InvariantCulture);
                                        ConfigManager.SetProperty(PropertyItem.SoundEffects, soundEffects.ToString());
                                        ConfigManager.SetProperty(PropertyItem.LowestNoteId, target.LowestSentNote.ToString());
                                        ConfigManager.SetProperty(PropertyItem.AvaliableNoteCount, target.NoteCount.ToString());
                                        ConfigManager.SetProperty(PropertyItem.NoteCooldown, target.NoteCooldown.ToString());
                                        ConfigManager.SetProperty(PropertyItem.LowestPlayedNote, target.LowestPlayedNote.ToString());
                                        ConfigManager.SetProperty(PropertyItem.ForbidsChords, target.ForbidsChords.ToString());
                                        tsm.UpdateTrackSelectionForInstrument(oldInstrument);
                                        player.mordhauOutDevice.UpdateNoteIdBounds();
                                    }

                                    converter.Range = ConfigManager.GetIntegerProperty(PropertyItem.AvaliableNoteCount);
                                    converter.LowNote = ConfigManager.GetIntegerProperty(PropertyItem.LowestPlayedNote);
                                    converter.IsConversionEnabled = true;
                                    converter.SetDivision(player.sequence.Division);
                                    converter.AddTrack();
                                    converter.SetEnabledTracksInTrack(i, tsm.MidiTracks);
                                    converter.SetEnabledMidiChannelsInTrack(i, tsm.MidiChannels);

                                    converter.FillTrack(i, player.ExtractMidiContent());
                                }

                                SaveManager.WriteSaveFile(SaveManager.SaveFilePath + namingForm.textBoxPartName.Text, converter.GetPartitionToString());
                                index.SaveIndex();
                                PopulateIndexList();
                                // And put the instrument back
                                if (ConfigManager.GetIntegerProperty(PropertyItem.Instrument) != firstInstrument)
                                {
                                    int oldInstrument = ConfigManager.GetIntegerProperty(PropertyItem.Instrument);
                                    ConfigManager.SetProperty(PropertyItem.Instrument, firstInstrument.ToString());
                                    Instrument target = Instrument.Prefabs[firstInstrument];

                                    bool soundEffects = !target.Name.StartsWith("Mordhau", true, System.Globalization.CultureInfo.InvariantCulture);
                                    ConfigManager.SetProperty(PropertyItem.SoundEffects, soundEffects.ToString());
                                    ConfigManager.SetProperty(PropertyItem.LowestNoteId, target.LowestSentNote.ToString());
                                    ConfigManager.SetProperty(PropertyItem.AvaliableNoteCount, target.NoteCount.ToString());
                                    ConfigManager.SetProperty(PropertyItem.NoteCooldown, target.NoteCooldown.ToString());
                                    ConfigManager.SetProperty(PropertyItem.LowestPlayedNote, target.LowestPlayedNote.ToString());
                                    ConfigManager.SetProperty(PropertyItem.ForbidsChords, target.ForbidsChords.ToString());
                                    tsm.UpdateTrackSelectionForInstrument(oldInstrument);
                                    player.mordhauOutDevice.UpdateNoteIdBounds();
                                }
                                //}
                                //else
                                //{
                                //    SaveManager.WriteSaveFile(SaveManager.SaveFilePath + namingForm.textBoxPartName.Text, trackConverter.GetPartitionToString());
                                //    index.SaveIndex();
                                //    PopulateIndexList();
                                //    trackConverter = null;
                                //}
                            }
                        }
                    }
                }
                else if (namingForm.DialogResult == DialogResult.Yes)
                {
                    // They wanted to just add it as a track
                    if (trackConverter == null)
                        trackConverter = new LuteMod.Converter.MordhauConverter();
                    // These ranges and settings only matter for FillTrack.  So re-setting them each time isn't a problem
                    trackConverter.Range = ConfigManager.GetIntegerProperty(PropertyItem.AvaliableNoteCount);
                    trackConverter.LowNote = ConfigManager.GetIntegerProperty(PropertyItem.LowestPlayedNote);
                    trackConverter.IsConversionEnabled = true;
                    trackConverter.SetDivision(player.sequence.Division); // This one could be weird
                    trackConverter.AddTrack();
                    trackConverter.SetEnabledTracksInTrack(trackConverter.GetTrackCount() - 1, tsm.MidiTracks);
                    trackConverter.SetEnabledMidiChannelsInTrack(trackConverter.GetTrackCount() - 1, tsm.MidiChannels);

                    trackConverter.FillTrack(trackConverter.GetTrackCount() - 1, player.ExtractMidiContent());
                }
            }
            else
            {
                MessageBox.Show("The partition is empty");
            }

        }

        private void PartitionsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            WindowPositionUtils.UpdateBounds(PropertyItem.PartitionListPos, new Point() { X = Left, Y = Top });
            ConfigManager.SaveConfig();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(SaveManager.SaveFilePath);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog openSavFileDialog = new OpenFileDialog();
            string[] fileNames;
            openSavFileDialog.DefaultExt = "sav";
            openSavFileDialog.Filter = "SAV files|*.sav";
            openSavFileDialog.Title = "Open SAV file";
            openSavFileDialog.Multiselect = true;
            if (openSavFileDialog.ShowDialog() == DialogResult.OK)
            {
                fileNames = openSavFileDialog.FileNames;
                foreach (string fileName in fileNames)
                {
                    index.AddFileInIndex(fileName);
                }
                index.SaveIndex();
                PopulateIndexList();
            }
        }
    }
}
