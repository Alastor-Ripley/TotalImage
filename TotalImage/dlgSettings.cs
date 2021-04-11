﻿using System;
using System.Windows.Forms;

namespace TotalImage
{
    public partial class dlgSettings : Form
    {
        public dlgSettings()
        {
            InitializeComponent();
        }

        private void btnClearRecent_Click(object sender, System.EventArgs e)
        {
            Settings.ClearRecentImages();

#if NET5_0_OR_GREATER
            TaskDialog.ShowDialog(this, new TaskDialogPage()
            {
                Text = "List of recently opened images has been successfully cleared.",
                Heading = "Recent images cleared",
                Caption = "Success",
                Buttons =
                {
                    TaskDialogButton.OK
                },
                Icon = TaskDialogIcon.Information,
                DefaultButton = TaskDialogButton.OK
            });
#endif
        }

        private void btnReset_Click(object sender, System.EventArgs e)
        {
#if NET5_0_OR_GREATER
            TaskDialogButton result = TaskDialog.ShowDialog(this, new TaskDialogPage()
            {
                Text = "All settings will be reset to their default values.",
                Heading = "Are you sure you want to continue?",
                Caption = "Warning",
                Buttons =
                {
                    TaskDialogButton.Yes,
                    TaskDialogButton.No
                },
                Icon = TaskDialogIcon.Warning,
                DefaultButton = TaskDialogButton.No
            });

            if(result == TaskDialogButton.Yes)
            {
                Settings.LoadDefaults();
                Settings.Save();

                TaskDialog.ShowDialog(this, new TaskDialogPage()
                {
                    Text = "All settings were successfully reset to their default values.",
                    Heading = "Settings reset",
                    Caption = "Success",
                    Buttons =
                {
                    TaskDialogButton.OK
                },
                    Icon = TaskDialogIcon.Information,
                    DefaultButton = TaskDialogButton.OK
                });
            }
#elif NET48
            DialogResult result = MessageBox.Show("All settings will be reset to their default values. Are you sure you want to continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if(result == DialogResult.Yes)
            {
                Settings.LoadDefaults();
                Settings.Save();

                MessageBox.Show("All settings were successfully reset to their default values.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            }
#endif

            SyncUIWithSettings();
        }

        private void dlgSettings_Load(object sender, System.EventArgs e)
        {
            SyncUIWithSettings();
        }

        //Syncs the dialog UI with CurrentSettings
        private void SyncUIWithSettings()
        {
            txtExtractPath.Text = Settings.CurrentSettings.DefaultExtractPath;
            cbxOpenDir.Checked = Settings.CurrentSettings.OpenFolderAfterExtract;
            cbxShowCommandBar.Checked = Settings.CurrentSettings.ShowCommandBar;
            cbxShowStatusBar.Checked = Settings.CurrentSettings.ShowStatusBar;
            cbxShowDirectoryTree.Checked = Settings.CurrentSettings.ShowDirectoryTree;
            cbxShowHiddenItems.Checked = Settings.CurrentSettings.ShowHiddenItems;
            cbxShowDeletedItems.Checked = Settings.CurrentSettings.ShowDeletedItems;
            lstSortBy.SelectedIndex = Settings.CurrentSettings.FilesSortingColumn;
            lstSortOrder.SelectedIndex = (int)Settings.CurrentSettings.FilesSortOrder - 1;
            cbxExtractAsk.Checked = Settings.CurrentSettings.ExtractAlwaysAsk;
            cbxPreserveAttributes.Checked = Settings.CurrentSettings.ExtractPreserveAttributes;
            cbxPreserveDates.Checked = Settings.CurrentSettings.ExtractPreserveDates;

            switch (Settings.CurrentSettings.FilesView)
            {
                case View.LargeIcon: lstViewType.SelectedIndex = 0; break;
                case View.SmallIcon: lstViewType.SelectedIndex = 1; break;
                case View.List: lstViewType.SelectedIndex = 2; break;
                case View.Details: lstViewType.SelectedIndex = 3; break;
                case View.Tile: lstViewType.SelectedIndex = 4; break;
            }

            switch (Settings.CurrentSettings.SizeUnits)
            {
                case Settings.SizeUnit.B: lstSizeUnits.SelectedIndex = 0; break;
                case Settings.SizeUnit.KB: lstSizeUnits.SelectedIndex = 1; break;
                case Settings.SizeUnit.KiB: lstSizeUnits.SelectedIndex = 2; break;
                case Settings.SizeUnit.MB: lstSizeUnits.SelectedIndex = 3; break;
                case Settings.SizeUnit.MiB: lstSizeUnits.SelectedIndex = 4; break;
            }

            switch (Settings.CurrentSettings.DefaultExtractType)
            {
                case Settings.FolderExtract.Ignore: rbnIgnoreFolders.Checked = true; break;
                case Settings.FolderExtract.Merge: rbnExtractFlat.Checked = true; break;
                case Settings.FolderExtract.Preserve: rbnExtractPreserve.Checked = true; break;
            }

            //CheckFileAssociations();
        }

        //TODO: This method should check the registry for current file associations and update the UI accordingly
        private void CheckFileAssociations()
        {
            throw new NotImplementedException();
        }

        private void btnBrowse_Click(object sender, System.EventArgs e)
        {
            using FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = true;

#if NET48
            //We only do this for .NET Framework because it has the old FBD and there's a notable empty space at the top without the description.
            //Meanwhile, .NET 5 has the new Vista+ FBD which doesn't handle the description well visually, especially if dark Explorer theme is used.
            fbd.Description = "Select a folder where extracted files will be saved.";
#endif

            if (fbd.ShowDialog() == DialogResult.OK)
            {
                txtExtractPath.Text = fbd.SelectedPath;
            }
        }

        //Sync CurrentSettings with the UI state and save to file
        private void btnOK_Click(object sender, System.EventArgs e)
        {
            Settings.CurrentSettings.DefaultExtractPath = txtExtractPath.Text;
            Settings.CurrentSettings.FilesSortingColumn = lstSortBy.SelectedIndex;
            Settings.CurrentSettings.FilesSortOrder = (SortOrder)(lstSortOrder.SelectedIndex + 1);
            Settings.CurrentSettings.OpenFolderAfterExtract = cbxOpenDir.Checked;
            Settings.CurrentSettings.ShowCommandBar = cbxShowCommandBar.Checked;
            Settings.CurrentSettings.ShowDeletedItems = cbxShowDeletedItems.Checked;
            Settings.CurrentSettings.ShowDirectoryTree = cbxShowDirectoryTree.Checked;
            Settings.CurrentSettings.ShowHiddenItems = cbxShowHiddenItems.Checked;
            Settings.CurrentSettings.ShowStatusBar = cbxShowStatusBar.Checked;
            Settings.CurrentSettings.ExtractAlwaysAsk = cbxExtractAsk.Checked;
            Settings.CurrentSettings.ExtractPreserveAttributes = cbxPreserveAttributes.Checked;
            Settings.CurrentSettings.ExtractPreserveDates = cbxPreserveDates.Checked;

            switch (lstSizeUnits.SelectedIndex)
            {
                case 0: Settings.CurrentSettings.SizeUnits = Settings.SizeUnit.B; break;
                case 1: Settings.CurrentSettings.SizeUnits = Settings.SizeUnit.KB; break;
                case 2: Settings.CurrentSettings.SizeUnits = Settings.SizeUnit.KiB; break;
                case 3: Settings.CurrentSettings.SizeUnits = Settings.SizeUnit.MB; break;
                case 4: Settings.CurrentSettings.SizeUnits = Settings.SizeUnit.MiB; break;
            }

            switch (lstViewType.SelectedIndex)
            {
                case 0: Settings.CurrentSettings.FilesView = View.LargeIcon; break;
                case 1: Settings.CurrentSettings.FilesView = View.SmallIcon; break;
                case 2: Settings.CurrentSettings.FilesView = View.List; break;
                case 3: Settings.CurrentSettings.FilesView = View.Details; break;
                case 4: Settings.CurrentSettings.FilesView = View.Tile; break;
            }

            if (rbnExtractFlat.Checked)
                Settings.CurrentSettings.DefaultExtractType = Settings.FolderExtract.Merge;
            else if (rbnExtractPreserve.Checked)
                Settings.CurrentSettings.DefaultExtractType = Settings.FolderExtract.Preserve;
            else if (rbnIgnoreFolders.Checked)
                Settings.CurrentSettings.DefaultExtractType = Settings.FolderExtract.Ignore;

            Settings.Save();
        }

        private void lstFileTypes_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (e.Item.Selected)
            {
                e.Item.Checked = !e.Item.Checked;

                if (lstFileTypes.CheckedItems.Count == lstFileTypes.Items.Count)
                {
                    cbxSelectAll.CheckState = CheckState.Checked;
                    cbxSelectAll.Checked = true;
                }
                else if (lstFileTypes.CheckedItems.Count == 0)
                {
                    cbxSelectAll.CheckState = CheckState.Unchecked;
                    cbxSelectAll.Checked = false;
                }
                else
                {
                    cbxSelectAll.CheckState = CheckState.Indeterminate;
                    cbxSelectAll.Checked = true;
                }
            }
        }

        private void lstFileTypes_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (lstFileTypes.CheckedItems.Count == lstFileTypes.Items.Count)
            {
                cbxSelectAll.CheckState = CheckState.Checked;
                cbxSelectAll.Checked = true;
            }
            else if (lstFileTypes.CheckedItems.Count == 0)
            {
                cbxSelectAll.CheckState = CheckState.Unchecked;
                cbxSelectAll.Checked = false;
            }
            else
            {
                cbxSelectAll.CheckState = CheckState.Indeterminate;
                cbxSelectAll.Checked = true;
            }
        }

        private void cbxSelectAll_Click(object sender, System.EventArgs e)
        {
            if (cbxSelectAll.Checked)
            {
                foreach (ListViewItem lvi in lstFileTypes.Items)
                {
                    lvi.Selected = false;
                    lvi.Checked = true;
                }
            }
            else if (!cbxSelectAll.Checked)
            {
                foreach (ListViewItem lvi in lstFileTypes.Items)
                {
                    lvi.Selected = false;
                    lvi.Checked = false;
                }
            }
        }
    }
}