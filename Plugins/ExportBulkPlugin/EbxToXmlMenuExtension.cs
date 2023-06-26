using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;

namespace EbxToXmlPlugin
{
    public class BulkExporting
    {
        public static string TopLevel = "Tools";

        public static string SubLevel = "Export Bulk";

        public static ImageSource pluginimageSource = new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Database.png") as ImageSource;
        public class EbxToXmlMenuExtension : MenuExtension
        {
            public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/EbxToXmlPlugin;component/Images/EbxToXml.png") as ImageSource;

            public override string TopLevelMenuName => TopLevel;
            public override string SubLevelMenuName => SubLevel;

            public override string MenuItemName => "EBX to XML";

            public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
            {
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                dialog.InitialDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    string outDir = dialog.FileName;
                    FrostyTaskWindow.Show("Exporting EBX", "", (task) =>
                    {
                        uint totalCount = App.AssetManager.GetEbxCount();
                        uint idx = 0;

                        foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx())
                        {
                            task.Update(entry.Name, (idx++ / (double)totalCount) * 100.0d);

                            string fullPath = outDir + "/" + entry.Path + "/";

                            string filename = entry.Filename + ".xml";
                            filename = string.Concat(filename.Split(Path.GetInvalidFileNameChars()));

                            if (File.Exists(fullPath + filename))
                                continue;

                            try
                            {
                                DirectoryInfo di = new DirectoryInfo(fullPath);
                                if (!di.Exists)
                                    Directory.CreateDirectory(di.FullName);

                                EbxAsset asset = App.AssetManager.GetEbx(entry);
                                using (EbxXmlWriter writer = new EbxXmlWriter(new FileStream(fullPath + filename, FileMode.Create), App.AssetManager))
                                    writer.WriteObjects(asset.Objects);
                            }
                            catch (Exception)
                            {
                                App.Logger.Log("Failed to export {0}", entry.Filename);
                            }
                        }
                    });

                    FrostyMessageBox.Show("Successfully exported EBX to " + outDir, "Frosty Editor");
                }
            });
        }

        public class EbxToBinMenuExtension : MenuExtension
        {
            public override ImageSource Icon => pluginimageSource;

            public override string TopLevelMenuName => TopLevel;
            public override string SubLevelMenuName => SubLevel;

            public override string MenuItemName => "EBX to BIN";

            public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
            {
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                dialog.InitialDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    string outDir = dialog.FileName;
                    FrostyTaskWindow.Show("Exporting EBX", "", (task) =>
                    {
                        uint totalCount = App.AssetManager.GetEbxCount();
                        uint idx = 0;

                        foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx())
                        {
                            task.Update(entry.Name, (idx++ / (double)totalCount) * 100.0d);

                            string fullPath = outDir + "/" + entry.Path + "/";

                            string filename = entry.Filename + ".bin";
                            filename = string.Concat(filename.Split(Path.GetInvalidFileNameChars()));

                            if (File.Exists(fullPath + filename))
                                continue;

                            try
                            {
                                DirectoryInfo di = new DirectoryInfo(fullPath);
                                if (!di.Exists)
                                    Directory.CreateDirectory(di.FullName);
                                using (NativeWriter writer = new NativeWriter(new FileStream(fullPath + filename, FileMode.Create), false, true))
                                {
                                    using (NativeReader reader = new NativeReader(App.AssetManager.GetEbxStream(entry)))
                                        writer.Write(reader.ReadToEnd());
                                }

                            }
                            catch (Exception)
                            {
                                App.Logger.Log("Failed to export {0}", entry.Filename);
                            }
                        }
                    });

                    FrostyMessageBox.Show("Successfully exported EBX to " + outDir, "Frosty Editor");
                }
            });
        }

        public class ChunksMenuExtension : MenuExtension
        {
            public override ImageSource Icon => pluginimageSource;

            public override string TopLevelMenuName => TopLevel;
            public override string SubLevelMenuName => SubLevel;

            public override string MenuItemName => "Chunks";

            public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
            {
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                dialog.InitialDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    string outDir = dialog.FileName;
                    FrostyTaskWindow.Show("Exporting Chunks", "", (task) =>
                    {
                        uint totalCount = (uint)App.AssetManager.EnumerateChunks().ToList().Count;
                        uint idx = 0;

                        foreach (ChunkAssetEntry entry in App.AssetManager.EnumerateChunks())
                        {
                            task.Update(entry.Name, (idx++ / (double)totalCount) * 100.0d);

                            string fullPath = outDir + "/" + entry.Path + "/";

                            string filename = entry.Filename + ".chunk";
                            filename = string.Concat(filename.Split(Path.GetInvalidFileNameChars()));

                            if (File.Exists(fullPath + filename))
                                continue;

                            try
                            {
                                DirectoryInfo di = new DirectoryInfo(fullPath);
                                if (!di.Exists)
                                    Directory.CreateDirectory(di.FullName);
                                using (NativeWriter writer = new NativeWriter(new FileStream(fullPath + filename, FileMode.Create), false, true))
                                {
                                    using (NativeReader reader = new NativeReader(App.AssetManager.GetChunk(entry)))
                                        writer.Write(reader.ReadToEnd());
                                }

                            }
                            catch (Exception)
                            {
                                App.Logger.Log("Failed to export {0}", entry.Filename);
                            }
                        }
                    });

                    FrostyMessageBox.Show("Successfully exported chunk to " + outDir, "Frosty Editor");
                }
            });
        }

        public class ResMenuExtension : MenuExtension
        {
            public override ImageSource Icon => pluginimageSource;

            public override string TopLevelMenuName => TopLevel;
            public override string SubLevelMenuName => SubLevel;

            public override string MenuItemName => "Res";

            public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
            {
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                dialog.InitialDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    string outDir = dialog.FileName;
                    FrostyTaskWindow.Show("Exporting Res", "", (task) =>
                    {
                        uint totalCount = (uint)App.AssetManager.EnumerateRes().ToList().Count;
                        uint idx = 0;

                        foreach (ResAssetEntry entry in App.AssetManager.EnumerateRes())
                        {
                            task.Update(entry.Name, (idx++ / (double)totalCount) * 100.0d);

                            string fullPath = outDir + "/" + entry.Path + "/";

                            string filename = entry.Filename + ".res";
                            filename = string.Concat(filename.Split(Path.GetInvalidFileNameChars()));

                            if (File.Exists(fullPath + filename))
                                continue;

                            try
                            {
                                DirectoryInfo di = new DirectoryInfo(fullPath);
                                if (!di.Exists)
                                    Directory.CreateDirectory(di.FullName);
                                using (NativeWriter writer = new NativeWriter(new FileStream(fullPath + filename, FileMode.Create), false, true))
                                {
                                    using (NativeReader reader = new NativeReader(App.AssetManager.GetRes(entry)))
                                        writer.Write(reader.ReadToEnd());
                                }

                            }
                            catch (Exception)
                            {
                                App.Logger.Log("Failed to export {0}", entry.Name);
                            }
                        }
                    });

                    FrostyMessageBox.Show("Successfully exported res to " + outDir, "Frosty Editor");
                }
            });
        }

        public class HashesMenuExtension : MenuExtension
        {
            internal static ImageSource imageSource = pluginimageSource;

            public override string TopLevelMenuName => TopLevel;
            public override string SubLevelMenuName => SubLevel;

            public override string MenuItemName => "Hashes List";
            public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Properties.png") as ImageSource;

            public Dictionary<CString, int> Hashes = new Dictionary<CString, int>();
            public static bool HasProperty(object obj, string propertyName)
            {
                return obj.GetType().GetProperty(propertyName) != null;
            }
            public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
            {
                FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save Localized Strings Usage List", "*.txt (Text File)|*.txt", "LocalizedStringsUsage");
                if (sfd.ShowDialog())
                {
                    string outDir = sfd.FileName;
                    FrostyTaskWindow.Show("Exporting Hash List", "", (task) =>
                    {
                        uint totalCount = App.AssetManager.GetEbxCount();
                        uint idx = 0;

                        foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx())
                        {
                            //if (entry.Name == "UI/Frontend/Screens/Play/InstantAction/InstantActionSettingsScreen")
                            //{
                            task.Update(entry.Name, (idx++ / (double)totalCount) * 100.0d);

                            EbxAsset refAsset = App.AssetManager.GetEbx(entry);
                            dynamic refRoot = refAsset.RootObject;

                            foreach (dynamic obj in refAsset.Objects)
                            {
                                if (HasProperty(obj, "PropertyConnections"))
                                {
                                    foreach (dynamic PorpertyConnection in obj.PropertyConnections)
                                    {
                                        foreach (CString Field in new List<dynamic> { PorpertyConnection.SourceField, PorpertyConnection.TargetField })
                                        {
                                            if (!Hashes.ContainsKey(Field))
                                            {
                                                Hashes.Add(Field, 1);
                                            }
                                            else
                                            {
                                                Hashes[Field]++;
                                            }
                                        }
                                    }
                                }
                                if (HasProperty(obj, "LinkConnections"))
                                {
                                    foreach (dynamic LinkConnection in obj.LinkConnections)
                                    {
                                        foreach (CString Field in new List<dynamic> { LinkConnection.SourceField, LinkConnection.TargetField })
                                        {
                                            if (!Hashes.ContainsKey(Field))
                                            {
                                                Hashes.Add(Field, 1);
                                            }
                                            else
                                            {
                                                Hashes[Field]++;
                                            }
                                        }
                                    }
                                }
                                if (HasProperty(obj, "EventConnections"))
                                {
                                    foreach (dynamic EventConnection in obj.EventConnections)
                                    {
                                        foreach (CString Field in new List<dynamic> { EventConnection.SourceEvent.Name, EventConnection.TargetEvent.Name })
                                        {
                                            if (!Hashes.ContainsKey(Field))
                                            {
                                                Hashes.Add(Field, 1);
                                            }
                                            else
                                            {
                                                Hashes[Field]++;
                                            }
                                        }
                                    }
                                }

                            }
                            //foreach (CString key in Hashes.Keys)
                            //{
                            //    App.Logger.Log("Key: " + key + " " + Hashes[key].ToString());
                            //}
                            //}
                        }
                        List<string> UnknownHashes = new List<string>();
                        List<string> KnownHashes = new List<string>();
                        foreach (CString key in Hashes.Keys)
                        {
                            if (key.ToString().Length == 10 & key.ToString().StartsWith("0x"))
                            {
                                UnknownHashes.Add(key + "," + Hashes[key].ToString());
                            }
                            else
                            {
                                KnownHashes.Add(key + "," + Hashes[key].ToString());
                            }
                        }
                        UnknownHashes.Sort();
                        KnownHashes.Sort();
                        using (NativeWriter writer = new NativeWriter(new FileStream(outDir, FileMode.Create), false, true))
                        {
                            writer.WriteLine("Unknown Strings:");
                            foreach (string str in UnknownHashes)
                            {
                                writer.WriteLine(str);
                            }
                            writer.WriteLine("\nKnown Strings:");
                            foreach (string str in KnownHashes)
                            {
                                writer.WriteLine(str);
                            }
                        }
                    });

                    FrostyMessageBox.Show("Successfully exported Hashes List to " + outDir, "Frosty Editor");
                }
            });
        }
    }
}
