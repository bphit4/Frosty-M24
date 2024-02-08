using Frosty.Controls;
using Frosty.Core.Controls;
using FrostySdk.Managers.Entries;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace Frosty.Core.Windows
{
    public partial class BatchExportSelectedWindow : FrostyDockableWindow
    {
        private AssetPath selectedPath = null;
        private IEnumerable itemsSource = null;
        public bool includeSubDirectories { get; set; } = false;
        public bool exportModifiedOnly { get; set; } = false;
        public bool exportUnmodifiedOnly { get; set; } = false;

        public List<string> types = new List<string>()
        {
            "SkinnedMeshAsset",
            "RigidMeshAsset",
            "CompositeMeshAsset",
            "TextureAsset",
            "SoundWaveAsset"
        };

        public BatchExportSelectedWindow(AssetPath selectedPath, IEnumerable itemsSource)
        {
            InitializeComponent();

            this.selectedPath = selectedPath;
            this.itemsSource = itemsSource;
        }

        private void doneButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            FrostyFolderBrowserDialog fbd = new FrostyFolderBrowserDialog("Batch Export Location");
            if (fbd.ShowDialog())
            {
                string path = fbd.SelectedPath;
                string fullPath = selectedPath.FullPath.Trim('/');

                var stopWatch = new Stopwatch();
                stopWatch.Start();

                List<EbxAssetEntry> entries = new List<EbxAssetEntry>();
                foreach (EbxAssetEntry entry in itemsSource)
                {
                    bool pathMatches = includeSubDirectories ? entry.Path.ToLower().Contains(fullPath.ToLower()) : entry.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase);
                    bool typeMatches = types.Contains(entry.Type);
                    bool shouldExport = false;

                    // Adjusted logic to export all assets if neither checkbox is checked
                    if ((exportModifiedOnly && entry.IsModified) || (exportUnmodifiedOnly && !entry.IsModified) || (!exportModifiedOnly && !exportUnmodifiedOnly))
                    {
                        shouldExport = true;
                    }

                    if (pathMatches && typeMatches && shouldExport)
                    {
                        entries.Add(entry);
                    }
                }

                int initialCount = entries.Count;

                int i = 0;
                while (entries.Count > 0 && i < entries.Count)
                {
                    int originalCount = entries.Count;
                    AssetDefinition assetDefinition = App.PluginManager.GetAssetDefinition(entries[i].Type) ?? new AssetDefinition();
                    List<EbxAssetEntry> leftOverEntries = assetDefinition.BatchExport(entries, path, stopWatch);

                    if (leftOverEntries.Count == originalCount)
                    {
                        Console.WriteLine("Failed to export a group of asset definitions.");
                        i++;
                    }

                    entries = leftOverEntries;
                }

                stopWatch.Stop();

                int finalCount = entries.Count;
                int totalExported = initialCount - finalCount;

                var ts = stopWatch.Elapsed;
                string elapsedTime = String.Format("{0}.{1}", ts.Seconds, ts.Milliseconds);

                App.Logger.Log("Successfully exported {0} assets in {1} seconds.", totalExported, elapsedTime);
            }
            Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InstanceNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
