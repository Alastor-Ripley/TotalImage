﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Text.Json;
using System.IO;
using System.Diagnostics;

namespace TotalImage
{
    //This class is a singleton, thread-safe with a double check lock
    public sealed class Settings
    {
        private static Settings _instance;
        private static readonly object _lock = new object();

        public static Settings GetInstance()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = Load();
                    }
                }
            }
            return _instance;
        }

        public Settings() { }

        private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TotalImage");

        public View FilesView { get; set; }
        public int FilesSortingColumn { get; set; }
        public SortOrder FilesSortOrder { get; set; }
        public List<string> RecentImages { get; private set; } = new List<string>();
        public bool ShowHiddenItems { get; set; }
        public bool ShowDeletedItems { get; set; }
        public bool ShowCommandBar { get; set; }
        public bool ShowDirectoryTree { get; set; }
        public bool ShowFileList { get; set; }
        public bool ShowStatusBar { get; set; }
        public SizeUnit SizeUnits { get; set; }
        public string? DefaultExtractPath { get; set; }
        public FolderExtract DefaultExtractType { get; set; }
        public bool OpenFolderAfterExtract { get; set; }

        public enum SizeUnit
        {
            B = 1,
            KB = 1000,
            KiB = 1024,
            MB = 1000000,
            MiB = 1048576
        }

        //Default folder options for extraction
        public enum FolderExtract
        {
            Ignore,   //Folders will be ignored by default
            Merge,    //All files will be extracted into the same directory
            Preserve, //Directory structure will be preserved
            AlwaysAsk //The user will always be prompted with the Extract dialog
        }

        //TODO: Implement loading the values from permanent storage
        private static Settings Load()
        {
            //If the file doesn't exist (yet), load default values and create the file
            if (!File.Exists(Path.Combine(SettingsPath, "settings.json")))
            {
                //Also create the directory if even that doesn't exist (yet)
                if (!Directory.Exists(SettingsPath))
                {
                    Directory.CreateDirectory(SettingsPath);
                }

                Settings defaults = new Settings();
                defaults.LoadDefaults();
                defaults.Save();
                return defaults;
            }

            string json = File.ReadAllText(Path.Combine(SettingsPath, "settings.json"));
            Settings loaded = JsonSerializer.Deserialize<Settings>(json);
            if(loaded != null)
            {
                Debug.WriteLine("Loaded values:");
                Debug.WriteLine("FilesView: " + loaded.FilesView);
                Debug.WriteLine("RecentImages list: ");
                foreach (string s in loaded.RecentImages)
                {
                    Debug.WriteLine("-recent image: " + s);
                }
                return loaded;
            }
            else
            {
                throw new NullReferenceException();
            }
        }

        //TODO: Load default values for all the settings
        public void LoadDefaults()
        {
            //Set all settings to a default value here
            OpenFolderAfterExtract = true;
            DefaultExtractType = FolderExtract.AlwaysAsk;
            DefaultExtractPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            SizeUnits = SizeUnit.B;
            ShowCommandBar = true;
            ShowDeletedItems = false;
            ShowDirectoryTree = true;
            ShowFileList = true;
            ShowHiddenItems = true;
            ShowStatusBar = true;
            FilesSortingColumn = 0;
            FilesSortOrder = SortOrder.Ascending;
            FilesView = View.Details;
        }

        //TODO: Implement saving the values to permanent storage
        public void Save()
        {
            string json = JsonSerializer.Serialize(this);

            //Just in case...
            if (!Directory.Exists(SettingsPath))
            {
                Directory.CreateDirectory(SettingsPath);
            }

            File.WriteAllText(Path.Combine(SettingsPath, "settings.json"), json);
        }

        public void AddRecentImage(string path)
        {
            //This prevents duplicate entries by removing the old entry first - the new one is then put at the start of the list
            if (RecentImages.Count > 0)
            {
                if (RecentImages.LastIndexOf(path) > -1)
                    RecentImages.RemoveAt(RecentImages.LastIndexOf(path));
            }

            //10 entries seems like a reasonable number
            if (RecentImages.Count >= 10)
                RecentImages.RemoveAt(0);
            RecentImages.Add(path);
        }
    }
}