using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Xml.Serialization;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Commands;
using Frosty.Core.Controls;
using Frosty.Core.Converters;
using Frosty.Core.Interfaces;
using Frosty.Core.Legacy;
using Frosty.Core.Mod;
using Frosty.Core.Windows;
using Frosty.ModSupport;
using FrostyCore;
using FrostySdk;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;
using Microsoft.Win32;
using Bookmarks = Frosty.Core.Bookmarks;

namespace FrostyEditor.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : IEditorWindow
    {
        public FrostyDataExplorer DataExplorer => dataExplorer;
        public FrostyDataExplorer LegacyExplorer => legacyExplorer;
        public FrostyDataExplorer VisibleExplorer => m_currentExplorer;
        public TabControl MiscTabControl => miscTabControl;

        public FrostyProject Project => m_project;
        
        private FrostyProject m_project;
        private System.Timers.Timer m_autoSaveTimer;
        private FrostyDataExplorer m_currentExplorer;

        public ItemDoubleClickCommand BookmarkItemDoubleClickCommand { get; private set; }
        private List<string> recentProjects = new List<string>();

        public MainWindow()
        {
            FrostySdk.Attributes.GlobalAttributes.DisplayModuleInClassId = Config.Get<bool>("DisplayModuleInId", false);
            BookmarkItemDoubleClickCommand = new ItemDoubleClickCommand(BookmarkTreeView_MouseDoubleClick);

            m_project = new FrostyProject();

            InitializeComponent();

            explorerTabContent.HeaderControl = explorerTabControl;
            tabContent.HeaderControl = TabControl;
            miscTabContent.HeaderControl = miscTabControl;
            RoutedCommand closeTabCmd = new RoutedCommand();
            closeTabCmd.InputGestures.Add(new KeyGesture(Key.W, ModifierKeys.Control));
            this.CommandBindings.Add(new CommandBinding(closeTabCmd, CloseCurrentTab));

            UpdateWindowTitle();

            RoutedCommand newCmd = new RoutedCommand();
            RoutedCommand openCmd = new RoutedCommand();
            RoutedCommand saveCmd = new RoutedCommand();
            RoutedCommand saveAsCmd = new RoutedCommand();
            RoutedCommand addBookmarkCmd = new RoutedCommand();
            RoutedCommand removeBookmarkCmd = new RoutedCommand();
            RoutedCommand launchGameCmd = new RoutedCommand();
            RoutedCommand focusAssetFilterCmd = new RoutedCommand();

            newCmd.InputGestures.Add(new KeyGesture(Key.N, ModifierKeys.Control));
            openCmd.InputGestures.Add(new KeyGesture(Key.O, ModifierKeys.Control));
            saveCmd.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Control));
            saveAsCmd.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift));
            addBookmarkCmd.InputGestures.Add(new KeyGesture(Key.B, ModifierKeys.Control));
            removeBookmarkCmd.InputGestures.Add(new KeyGesture(Key.B, ModifierKeys.Control | ModifierKeys.Shift));
            launchGameCmd.InputGestures.Add(new KeyGesture(Key.F5));
            focusAssetFilterCmd.InputGestures.Add(new KeyGesture(Key.F, ModifierKeys.Control));

            CommandBindings.Add(new CommandBinding(newCmd, newModMenuItem_Click));
            CommandBindings.Add(new CommandBinding(openCmd, openModMenuItem_Click));
            CommandBindings.Add(new CommandBinding(saveCmd, saveModMenuItem_Click));
            CommandBindings.Add(new CommandBinding(saveAsCmd, saveAsModMenuItem_Click));
            CommandBindings.Add(new CommandBinding(addBookmarkCmd, BookmarkAddButton_Click));
            CommandBindings.Add(new CommandBinding(removeBookmarkCmd, BookmarkRemoveButton_Click));
            CommandBindings.Add(new CommandBinding(focusAssetFilterCmd, (s, e) => dataExplorer.FocusFilter()));

            LoadRecentProjects();  // Add this line

            CommandBindings.Add(new CommandBinding(launchGameCmd, launchButton_Click));
            InputBindings.Add(new KeyBinding
            {
                Command = new RelayCommand(o => RemoveAllTabs()),
                Key = Key.D,
                Modifiers = ModifierKeys.Control
            });

            Bookmarks.BookmarkDb.ContextChanged += BookmarkDb_ContextChanged;
            BookmarkContextPicker.ItemsSource = Bookmarks.BookmarkDb.Contexts.Values;
            if (Bookmarks.BookmarkDb.CurrentContext != null)
            {
                Bookmarks.BookmarkDb.CurrentContext.TargetAvailable += CurrentBookmarkContext_TargetAvailable;
                Bookmarks.BookmarkDb.CurrentContext.TargetUnavailable += CurrentBookmarkContext_TargetUnavailable;
            }
            BookmarkContextPicker.SelectedItem = Bookmarks.BookmarkDb.CurrentContext;
            TaskbarItemInfo = new System.Windows.Shell.TaskbarItemInfo();

            m_currentExplorer = dataExplorer;
        }

        #region PluginExtensions
        private void LoadPluginExtensions()
        {
            InitGameSpecificMenus();

            LoadToolbarExtensions();
            LoadMenuExtensions();
            LoadTabExtensions();
            LoadDataExplorerMenuItemExtensions();
        }
        
        private void LoadToolbarExtensions()
        {
            IEnumerable<ToolbarItem> toolbarItems = App.PluginManager.ToolbarExtensions.Select(toolbarExtension => new ToolbarItem(toolbarExtension.Name, "", null, toolbarExtension.ToolbarItemClicked, true));
            EditorToolbarItems.ItemsSource = toolbarItems;
        }
        private void LoadMenuExtensions()
        {
            // Add menu extensions to Editor
            foreach (MenuExtension menuExtension in App.PluginManager.MenuExtensions)
            {
                // find top level menu if there is one, and create one if not
                MenuItem foundMenuItem = menu.Items.Cast<MenuItem>().FirstOrDefault(menuItem => menuExtension.TopLevelMenuName.Equals(menuItem.Header as string, StringComparison.OrdinalIgnoreCase));
                if (foundMenuItem == null)
                {
                    foundMenuItem = new MenuItem()
                    {
                        Header = menuExtension.TopLevelMenuName,
                        Tag = menuExtension
                    };
                    menu.Items.Add(foundMenuItem);
                }
                
                // find sub level menu if there is one, and create one if not
                if (!string.IsNullOrEmpty(menuExtension.SubLevelMenuName))
                {
                    MenuItem parentMenuItem = null;
                    foreach (object menuItem in foundMenuItem.Items)
                    {
                        if (menuItem is MenuItem item)
                        {
                            if (menuExtension.SubLevelMenuName.Equals(item.Header as string, StringComparison.OrdinalIgnoreCase))
                            {
                                parentMenuItem = foundMenuItem;
                                foundMenuItem = item;
                                break;
                            }
                        }
                    }

                    if (parentMenuItem == null)
                    {
                        parentMenuItem = foundMenuItem;
                        foundMenuItem = new MenuItem
                        {
                            Header = menuExtension.SubLevelMenuName,
                            Tag = menuExtension
                        };
                        parentMenuItem.Items.Add(foundMenuItem);
                    }
                }

                Control existingMenuExtItem = foundMenuItem.Items.Cast<Control>()
                    .Where(menuItem => menuItem.Tag != null)
                    .FirstOrDefault(menuItem => 
                        menuExtension.MenuItemName.Equals(
                            (menuItem.Tag as MenuExtension).MenuItemName, StringComparison.OrdinalIgnoreCase
                        )
                    );
                if (existingMenuExtItem == null)
                {
                    // create and add menu item to top level menu
                    MenuItem menuExtItem = new MenuItem
                    {
                        Header = menuExtension.MenuItemName,
                        Icon = new Image() { Source = menuExtension.Icon },
                        Command = menuExtension.MenuItemClicked,
                        Tag = menuExtension
                    };
                    foundMenuItem.Items.Add(menuExtItem);
                }
            }
        }
        private void LoadDataExplorerMenuItemExtensions()
        {
            bool dataExplorerMenuItemsAdded = false;
            
            foreach (DataExplorerContextMenuExtension contextItemExtension in App.PluginManager.DataExplorerContextMenuExtensions)
            {
                Control existingItem = dataExplorer.AssetContextMenu.Items.Cast<Control>()
                    .Where(menuItem => menuItem.Tag != null)
                    .FirstOrDefault(menuItem => 
                        contextItemExtension.ContextItemName.Equals(
                            (menuItem.Tag as DataExplorerContextMenuExtension).ContextItemName, StringComparison.OrdinalIgnoreCase
                        )
                    );
                if (existingItem == null)
                {
                    MenuItem contextMenuItem = new MenuItem
                    {
                        Header = contextItemExtension.ContextItemName,
                        Icon = new Image() { Source = contextItemExtension.Icon },
                        Command = contextItemExtension.ContextItemClicked,
                        Tag = contextItemExtension
                    };

                    if (!dataExplorerMenuItemsAdded)
                    {
                        dataExplorerMenuItemsAdded = true;
                        dataExplorer.AssetContextMenu.Items.Add(new Separator());
                    }

                    dataExplorer.AssetContextMenu.Items.Add(contextMenuItem);
                }
            }
        }
        private void LoadTabExtensions()
        {
            foreach (TabExtension tabExtension in App.PluginManager.TabExtensions)
            {
                FrostyTabItem existingItem = miscTabControl.Items.Cast<FrostyTabItem>().FirstOrDefault(menuItem => tabExtension.TabItemName.Equals(menuItem.Header as string, StringComparison.OrdinalIgnoreCase));
                if (existingItem == null)
                {
                    FrostyTabItem tabExtItem = new FrostyTabItem
                    {
                        Header = tabExtension.TabItemName,
                        Content = tabExtension.TabContent,
                        Icon = tabExtension.TabItemIcon,
                        CloseButtonVisible = false,
                        Tag = tabExtension
                    };

                    miscTabControl.Items.Add(tabExtItem);
                }
            }
        }
        #endregion
        
        
        private void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDiscordState();

            FrostyTabItem ti = TabControl.SelectedItem as FrostyTabItem;

            if (!(ti?.Content is FrostyAssetEditor editor))
                return;

            AssetEditorToolbarItems.ItemsSource = editor.RegisterToolbarItems();
        }

        private void UpdateDiscordState()
        {
            string state = "";

            if (TabControl.SelectedItem is FrostyTabItem ti)
            {
                string header = ti.Header as string;
                string tabId = ti.TabId;

                if (tabId != null)
                {
                    AssetEntry ebx = App.AssetManager.GetEbxEntry(tabId) ?? App.AssetManager.GetCustomAssetEntry("legacy", tabId);

                    var action = "Viewing";
                    var filename = "a file";

                    if (ebx != null)
                    {
                        filename = ebx.Filename;

                        if (ebx.IsDirty && ProfilesLibrary.EnableExecution)
                            action = "Editing";
                    }

                    state = action + " " + filename;
                }
                else
                    state = header;
            }

            App.UpdateDiscordRpc(state);
        }

        private void InitGameSpecificMenus()
        {
            if (ProfilesLibrary.IsLoaded(ProfileVersion.Fifa17, ProfileVersion.Fifa18,
                ProfileVersion.Madden19, ProfileVersion.Fifa19,
                ProfileVersion.Madden20, ProfileVersion.Fifa20,
                ProfileVersion.PlantsVsZombiesBattleforNeighborville, ProfileVersion.Fifa21,
                ProfileVersion.Madden22, ProfileVersion.Fifa22, ProfileVersion.Fifa23,
                ProfileVersion.Madden23, ProfileVersion.PGATour,
                ProfileVersion.Madden24))
            {
                InitFifaMenu();
            }

            if (ProfilesLibrary.IsLoaded(ProfileVersion.Madden24))
            {
                RestoreMadden.Visibility = Visibility.Visible;
            }
            else
            {
                RestoreMadden.Visibility = Visibility.Collapsed;
            }
        }

        private void InitFifaMenu()
        {
            legacyExplorerTabItem.Visibility = Visibility.Visible;
        }

        private void explorerTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            m_currentExplorer = Equals(explorerTabControl.SelectedItem, dataExplorerTabItem) ? dataExplorer : legacyExplorer;
        }

        private void UpdateWindowTitle()
        {
            Title = "Frosty Editor: MMC Edition - " + Frosty.Core.App.Version;

            if (ProfilesLibrary.HasLoadedProfile)
            {
                Title = Title + " (" + ProfilesLibrary.DisplayName + ") ";

                if (ProfilesLibrary.EnableExecution)
                    Title += "[" + m_project.DisplayName + "]";
                else
                    Title += "[Read Only]";
            }
        }

        private void FrostyWindow_Loaded(object sender, EventArgs e)
        {
            (App.Logger as FrostyLogger)?.AddBinding(tb, TextBox.TextProperty);

            // kick off autosave timer
            bool enabled = Config.Get<bool>("AutosaveEnabled", true);
            if (enabled)
            {
                int timerInterval = Config.Get<int>("AutosavePeriod", 5) * 60 * 1000;
                if (timerInterval > 0)
                {
                    m_autoSaveTimer = new System.Timers.Timer {Interval = timerInterval};
                    m_autoSaveTimer.Elapsed += AutoSaveTimer_Elapsed;
                    m_autoSaveTimer.AutoReset = false;
                    m_autoSaveTimer.Start();
                }
            }

            if (ProfilesLibrary.HasLoadedProfile)
            {
                Bookmarks.BookmarkDb.LoadDb();
            }

            // load profile through the project if it's apart of the launch args, if not choose on startup
            if (App.OpenProject)
            {
                LoadProject(App.LaunchArgs, false);
            }
            else
            {
                string selectedProfileName = FrostyProfileSelectWindow.Show();
                if (!string.IsNullOrEmpty(selectedProfileName) && SelectProfile(selectedProfileName))
                {
                    NewProject();
                    UpdateUI(true);
                }
            }
        }

        private void logTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (tb.IsFocused)
                tb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
            tb.ScrollToEnd();
        }

        private int lastSaveIndex;
        private void AutoSaveTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (m_project.IsDirty)
            {
                ++lastSaveIndex;
                if (lastSaveIndex > Config.Get<int>("AutosaveMaxCount", 10)) // Config.Get<int>("Autosave", "MaxCount", 10)
                    lastSaveIndex = 1;

                string projectName = m_project.DisplayName.Remove(m_project.DisplayName.Length - 10);
                FileInfo fi = new FileInfo("Autosave/" + projectName + "_" + lastSaveIndex.ToString("D3") + ".fbproject");

                App.Logger.Log("Initiated autosave of project to " + fi.FullName);

                // save project but dont update dirty state
                m_project.Save(overrideFilename: fi.FullName, updateDirtyState: false);
            }

            // begin timer again
            m_autoSaveTimer?.Start();
        }

        private void exitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void launchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ProfilesLibrary.EnableExecution)
                return;

            // setup ability to cancel the process
            CancellationTokenSource cancelToken = new CancellationTokenSource();

            Random r = new Random();
            string editorModName = $"EditorMod{r.Next(1000, 9999):D4}.fbmod";
            LaunchButton.IsEnabled = false;

            // get all mods
            List<string> modPaths = Directory.EnumerateFiles($"Mods/{ProfilesLibrary.ProfileName}/", "*.fbmod", SearchOption.AllDirectories).Select(Path.GetFileName).ToList();

            // create temporary editor mod
            ModSettings editorSettings = new ModSettings { Title = "Editor Mod", Author = "Frosty Editor", Version = "1", Category = "Editor" };

            // apply mod
            const string additionalArgs = "";
            FrostyModExecutor executor = new FrostyModExecutor();

            // Set pack
            App.SelectedPack = "Editor";

            try
            {
                // run mod applying process
                FrostyTaskWindow.Show("Launching", "", (task) => 
                {
                    try
                    {
                        foreach (ExecutionAction executionAction in App.PluginManager.ExecutionActions)
                            executionAction.PreLaunchAction(task.TaskLogger, PluginManagerType.Editor, cancelToken.Token);

                        task.Update("Exporting Mod");
                        ExportMod(editorSettings, $"Mods/{ProfilesLibrary.ProfileName}/{editorModName}", true);
                        modPaths.Add(editorModName);

                        // allow cancelling in case of a big mod (will cancel after processing the mod)
                        // @todo: add cancellation to different stages of mod exportation, to allow cancelling
                        //        at any stage of a large mod

                        cancelToken.Token.ThrowIfCancellationRequested();

                        task.Update("");
                        executor.Run(App.FileSystem, cancelToken.Token, task.TaskLogger, $"Mods/{ProfilesLibrary.ProfileName}/", App.SelectedPack, additionalArgs, modPaths.ToArray());

                        foreach (ExecutionAction executionAction in App.PluginManager.ExecutionActions)
                            executionAction.PostLaunchAction(task.TaskLogger, PluginManagerType.Editor, cancelToken.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // swollow

                        foreach (ExecutionAction executionAction in App.PluginManager.ExecutionActions)
                            executionAction.PostLaunchAction(task.TaskLogger, PluginManagerType.ModManager, cancelToken.Token);
                    }

                }, showCancelButton: true, cancelCallback: (task) => cancelToken.Cancel());
            }
            catch (OperationCanceledException)
            {
                // process was cancelled
                App.Logger.Log("Launch Cancelled");
                App.NotificationManager.Show("Launch Cancelled");
            }

            // remove editor mod
            FileInfo editorMod = new FileInfo($"Mods/{ProfilesLibrary.ProfileName}/{editorModName}");
            if (editorMod.Exists)
                editorMod.Delete();

            LaunchButton.IsEnabled = true;

            GC.Collect();
        }

        public void ExportMod(ModSettings modSettings, string filename, bool bSilent)
        {
            m_project.WriteToMod(filename, modSettings);
            if (!bSilent)
                App.Logger.Log("Mod saved to {0}", filename);
        }

        private void newModMenuItem_Click(object sender, RoutedEventArgs e)
        {
            NewProject();
        }
        
        private void reselectProfileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            PromptToSaveDirtyProject();
            string selectedProfileName = FrostyProfileSelectWindow.Show();
            if (!string.IsNullOrEmpty(selectedProfileName) && SelectProfile(selectedProfileName))
            {
                NewProject();
                UpdateUI(true);
            }
        }

        private static bool SelectProfile(string profile)
        {
            Frosty.Core.App.ClearProfileData();
            bool result = Frosty.Core.App.LoadProfile(profile);

            App.InitDiscordRpc();
            App.UpdateDiscordRpc("Initializing");

            return result;
        }

        private void UpdateUI(bool newProject = false)
        {
            if (m_project.RequiresNewProfile || newProject)
            {
                LoadedPluginsList.ItemsSource = App.PluginManager.LoadedPlugins;

                LoadPluginExtensions();

                dataExplorer.ItemsSource = App.AssetManager.EnumerateEbx();
                legacyExplorer.ItemsSource = App.AssetManager.EnumerateCustomAssets("legacy");
            }
            else
            {
                dataExplorer.RefreshItems();
                legacyExplorer.RefreshItems();
            }
            
            if(ProfilesLibrary.EnableExecution)
            {
                LaunchButton.IsEnabled = true;
            }
        }

        private void PromptToSaveDirtyProject()
        {
            if (m_project.IsDirty)
            {
                MessageBoxResult result = FrostyMessageBox.Show("Do you wish to save changes to " + m_project.DisplayName + "?", "Frosty Editor", MessageBoxButton.YesNoCancel);
                if (result == MessageBoxResult.Cancel)
                    return;

                if (result == MessageBoxResult.Yes)
                {
                    if (SaveProject(false))
                    {
                        FrostyTaskWindow.Show("Saving Project", m_project.Filename, (task) => m_project.Save());
                        App.Logger.Log("Project saved to {0}", m_project.Filename);
                    }
                }
            }
        }
        
        private void NewProject()
        {
            m_autoSaveTimer?.Stop();
            PromptToSaveDirtyProject();

            // close all open tabs
            RemoveAllTabs();

            // clear all modifications
            App.AssetManager.Reset();
            dataExplorer.RefreshAll();
            legacyExplorer.RefreshAll();

            // create a new blank project
            m_project = new FrostyProject();

            App.Logger.Log("New project started");
            App.NotificationManager.Show("New project started");

            UpdateWindowTitle();
            UpdateDiscordState();

            // reset show only modified flag
            dataExplorer.ShowOnlyModified = false;
            legacyExplorer.ShowOnlyModified = false;

            // reset show only unmodified flag
            dataExplorer.ShowOnlyUnModified = false;
            legacyExplorer.ShowOnlyUnModified = false;

            m_autoSaveTimer?.Start();
        }

        private void openModMenuItem_Click(object sender, RoutedEventArgs e)
        {
            bool savePrevProject = false;
            FrostyOpenFileDialog ofd = new FrostyOpenFileDialog("Open Project", "*.fbproject (Frosty Project)|*.fbproject", "Project");

            if (!ofd.ShowDialog())
                return; 
            
            if (m_project.IsDirty)
            {
                MessageBoxResult result = FrostyMessageBox.Show("Do you wish to save changes to " + m_project.DisplayName + "?", "Frosty Editor", MessageBoxButton.YesNoCancel);
                switch (result)
                {
                    case MessageBoxResult.Cancel:
                        return;
                    case MessageBoxResult.Yes:
                        savePrevProject = SaveProject(false);
                        break;
                    case MessageBoxResult.None:
                        break;
                    case MessageBoxResult.OK:
                        break;
                    case MessageBoxResult.No:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            LoadProject(ofd.FileName, savePrevProject);
        }

        private void saveModMenuItem_Click(object sender, RoutedEventArgs e)
        {
            m_autoSaveTimer?.Stop();
            if (SaveProject(false))
            {
                FrostyTaskWindow.Show("Saving Project", m_project.Filename, (task) => m_project.Save());

                dataExplorer.RefreshItems();
                legacyExplorer.RefreshItems();
                RefreshTabs();

                App.Logger.Log("Project saved to {0}", m_project.Filename);

                UpdateWindowTitle();
                UpdateDiscordState();
            }
            m_autoSaveTimer?.Start();
        }

        private void saveAsModMenuItem_Click(object sender, RoutedEventArgs e)
        {
            m_autoSaveTimer?.Stop();
            if (SaveProject(true))
            {
                FrostyTaskWindow.Show("Saving Project", m_project.Filename, (task) => m_project.Save());

                dataExplorer.RefreshItems();
                legacyExplorer.RefreshItems();
                RefreshTabs();

                App.Logger.Log("Project saved to {0}", m_project.Filename);

                UpdateWindowTitle();
                UpdateDiscordState();
            }
            m_autoSaveTimer?.Start();
        }

        private void LoadProject(string filename, bool saveProject)
        {
            m_autoSaveTimer?.Stop();

            if (saveProject)
            {
                m_project.Save();
                App.Logger.Log("Project saved to {0}", m_project.Filename);
            }

            // Load project
            FrostyProject newProject = new FrostyProject();
            if (!newProject.Load(filename))
            {
                if (ProfilesLibrary.DataVersion != newProject.gameVersion) // User loaded project for a different game
                {
                    App.Logger.LogWarning("Project {0} is not for {1}.", filename, ProfilesLibrary.DisplayName);
                }
                else // Corrupt or not a Frosty project
                {
                    App.Logger.LogWarning("Failed to load {0}", filename);
                }

                newProject = null;
            }

            if (newProject != null)
            {
                // Close all open tabs
                RemoveAllTabs();

                m_project = newProject;

                // Update UI
                UpdateUI();

                bool allFiles = Config.Get("ShowAllFiles", false);

                legacyExplorer.ShowOnlyModified = false;
                if(allFiles)
                {
                    legacyExplorer.ShowOnlyModified = false;
                }
                else
                {
                    legacyExplorer.ShowOnlyModified = true;
                }

                dataExplorer.ShowOnlyModified = false;
                if (allFiles)
                {
                    dataExplorer.ShowOnlyModified = false;
                }
                else
                {
                    dataExplorer.ShowOnlyModified = true;
                }

                legacyExplorer.ShowOnlyUnModified = false;
                legacyExplorer.ShowOnlyUnModified = false;

                dataExplorer.ShowOnlyUnModified = false;
                dataExplorer.ShowOnlyUnModified = false;

                // Report success
                App.Logger.Log("Loaded {0}", m_project.Filename);
                App.NotificationManager.Show($"Loaded {m_project.Filename}");

                // Update window title and Discord state
                UpdateWindowTitle();
                UpdateDiscordState();

                // Remember this project
                if (recentProjects.Contains(filename))
                {
                    recentProjects.Remove(filename);
                }
                recentProjects.Insert(0, filename);
                if (recentProjects.Count > 10)
                {
                    recentProjects.RemoveAt(10);  // Remove the oldest project to keep the list size to 10
                }
                SaveRecentProjects();

            }

            m_autoSaveTimer?.Start();
        }

        private void RecentProjectsMenuItem_MouseEnter(object sender, MouseEventArgs e)
        {
            RecentProjectsContextMenu.Items.Clear();

            // Assuming recentProjects is a List<string> containing the paths of recent projects
            foreach (var projectPath in recentProjects)
            {
                var menuItem = new MenuItem
                {
                    Header = System.IO.Path.GetFileName(projectPath),  // Display only the filename
                    Tag = projectPath  // Store the full path in the Tag property
                };

                menuItem.Click += (s, args) =>
                {
                    var selectedProjectPath = ((MenuItem)s).Tag as string;
                    if (!string.IsNullOrEmpty(selectedProjectPath))
                    {
                        LoadProject(selectedProjectPath, false);
                    }
                };

                RecentProjectsContextMenu.Items.Add(menuItem);
            }

            // Open the context menu manually since it's not directly attached to any MenuItem.
            RecentProjectsContextMenu.PlacementTarget = sender as UIElement;
            RecentProjectsContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;  // <-- Add this line
            RecentProjectsContextMenu.IsOpen = true;
        }

        private void SaveRecentProjects()
        {
            File.WriteAllLines("recent_projects.txt", recentProjects);
        }

        private void LoadRecentProjects()
        {
            if (File.Exists("recent_projects.txt"))
            {
                recentProjects = File.ReadAllLines("recent_projects.txt").ToList();
            }
        }

        private bool SaveProject(bool forceSaveAs)
        {
            if (m_project.Filename == "" || forceSaveAs)
            {
                FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save Project As", "*.fbproject (Frosty Project)|*.fbproject", "Project");
                if (!sfd.ShowDialog())
                    return false;

                m_project.Filename = sfd.FileName;
            }

            return true;
        }

        private void hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString());
        }

        public void OpenEditor(string title, FrostyBaseEditor editor)
        {
            FrostyTabItem ti = new FrostyTabItem();

            RoutedCommand closeCmd = new RoutedCommand();
            closeCmd.InputGestures.Add(new KeyGesture(Key.W, ModifierKeys.Control));

            ti.Header = title;
            ti.Content = editor;
            ti.Icon = editor.Icon;
            ti.CloseButtonVisible = true;
            ti.CloseButtonClick += (o, e2) => { RemoveTab(ti); };
            ti.MiddleMouseButtonClick += (s, o) => { RemoveTab(ti); };
            ti.IsSelected = true;
            ti.Focus();
            lastActiveTab = ti;

            AddTab(ti);
        }
        
        public AssetEntry GetOpenedAssetEntry()
        {
            FrostyTabItem firstTabItem = TabControl.SelectedItem as FrostyTabItem;
            if (firstTabItem?.Content is FrostyAssetEditor editor)
            {
                return editor.AssetEntry;
            }

            return null;
        }

        public void OpenAsset(AssetEntry asset, bool shouldCreateDefaultEditor = true, bool openOriginal = false)
        {
            var assetName = asset.Name;
            var assetDisplayName = asset.DisplayName;

            if (openOriginal == true)
            {
                var originalSuffix = " [ORIGINAL]";
                assetName += originalSuffix;
                assetDisplayName += originalSuffix;
                assetDisplayName = assetDisplayName.Replace("*", "");   // don't show the original as modified.
            }

            if (asset == null)
            {
                return;
            }

            if (asset.Type == "EncryptedAsset")
            {
                return;
            }

            foreach (FrostyTabItem currentTi in TabControl.Items)
            {
                if (currentTi.TabId == assetName)
                {
                    currentTi.IsSelected = true;
                    return;
                }
            }

            FrostyTabItem ti = new FrostyTabItem();
            FrostyAssetEditor editor = null;

            AssetDefinition definition = App.PluginManager.GetAssetDefinition(asset.Type);
            if (definition != null)
            {
                editor = definition.GetEditor(App.Logger);
            }
            
            if (editor == null)
            {
                if (!shouldCreateDefaultEditor)
                {
                    return;
                }

                editor = new FrostyBaseAssetEditor(App.Logger);
            }

            try
            {
                editor.SetAsset(asset, openOriginal);
            }
            catch (Exception)
            {
                FrostyMessageBox.Show("Unable to open asset", "Frosty Editor");
                return;
            }

            editor.OnAssetModified += (s, o) =>
            {
                // update UI
                ti.Header = asset.DisplayName;
                ti.Icon = new AssetEntryToBitmapSourceConverter().Convert(asset, typeof(ImageSource), null, null) as ImageSource;
                dataExplorer.RefreshItems();
                legacyExplorer.RefreshItems();

                UpdateDiscordState();
            };

            RoutedCommand closeCmd = new RoutedCommand();
            closeCmd.InputGestures.Add(new KeyGesture(Key.W, ModifierKeys.Control));

            ti.Icon = (new AssetEntryToBitmapSourceConverter().Convert(asset, typeof(ImageSource), null, null) as ImageSource);
            ti.Content = editor;
            ti.Header = assetDisplayName;
            ti.TabId = assetName;
            ti.IsSelected = true;
            ti.Focus();
            lastActiveTab = ti;
            ti.CloseButtonVisible = true;
            ti.CloseButtonClick += (s, o) =>
            {
                ShutdownEditorAndRemoveTab(editor, ti);
            };
            ti.MiddleMouseButtonClick += (s, o) =>
            {
                ShutdownEditorAndRemoveTab(editor, ti);
            };

            AddTab(ti);
        }
        
        private FrostyTabItem lastActiveTab;

        private void AddTab(FrostyTabItem ti)
        {
            TabControl.Items.Add(ti);
            FrostyTabItem welcomeTabItem = TabControl.Items[0] as FrostyTabItem;
            welcomeTabItem.Visibility = Visibility.Collapsed;
        }

        private void ShutdownEditorAndRemoveTab(FrostyAssetEditor editor, FrostyTabItem ti)
        {
            editor.Closed();
            
            if (ti.IsSelected)
            {
                AssetEditorToolbarItems.ItemsSource = null;
            }
            
            TabControl.Items.Remove(ti);
            if (TabControl.Items.Count == 1)
            {
                FrostyTabItem item = TabControl.Items[0] as FrostyTabItem;

                item.Visibility = Visibility.Visible;
                item.IsSelected = true;
            }
        }

        private void RemoveTab(FrostyTabItem ti)
        {
            FrostyBaseEditor editor = ti.Content as FrostyBaseEditor;
            editor?.Closed();
            FrostyAssetEditor assetEditor = ti.Content as FrostyAssetEditor;
            assetEditor?.Closed();

            // Remove the selected tab
            TabControl.Items.Remove(ti);

            // Check if there are any other tabs left
            if (TabControl.Items.Count > 1)
            {
                // If lastActiveTab is null, it means this was the first tab.
                // In that case, make the new first tab active.
                if (lastActiveTab == null)
                {
                    FrostyTabItem item = TabControl.Items[0] as FrostyTabItem;
                    item.Visibility = Visibility.Visible;
                    item.IsSelected = true;
                }
                else
                {
                    // Make the last active tab the new active tab
                    lastActiveTab.IsSelected = true;
                }
            }
            else
            {
                lastActiveTab = null; // No more tabs, set to null
            }
        }

        private void RemoveAllTabs()
        {
            while (TabControl.Items.Count > 1)
            {
                FrostyTabItem tabItem = TabControl.Items[1] as FrostyTabItem;
                if (tabItem.Content is FrostyAssetEditor editor)
                {
                    ShutdownEditorAndRemoveTab(editor, tabItem);
                }
                else
                {
                    RemoveTab(tabItem);
                }
            }
        }

        private void CloseCurrentTab(object sender, ExecutedRoutedEventArgs e)
        {
            App.Logger.Log("CloseCurrentTab called.");

            // Get the currently selected tab from the TabControl
            FrostyTabItem selectedTab = TabControl.SelectedItem as FrostyTabItem;

            if (selectedTab != null)
            {
                App.Logger.Log($"Closing tab: {selectedTab.Header}");
                RemoveTab(selectedTab); // RemoveTab takes care of all the cleanup
            }
            else
            {
                App.Logger.Log("No active tab to close.");
            }
        }


        private void RefreshTabs()
        {
            foreach (FrostyTabItem item in TabControl.Items)
            {
                if (item.Content is FrostyAssetEditor editor)
                {
                    if (editor.AssetEntry != null && !editor.IsReadOnly)
                        item.Header = editor.AssetEntry.DisplayName;
                }
            }
        }

        private void TabItem_MouseMove(object sender, MouseEventArgs e) {
            if (!(e.Source is FrostyTabItem tabItem)) {
                return;
            }

            if (Mouse.PrimaryDevice.LeftButton == MouseButtonState.Pressed) {
                DragDrop.DoDragDrop(tabItem, tabItem, DragDropEffects.All);
            }
        }

        private void TabItem_Move(object sender, DragEventArgs e)
        {
            if (!(e.Source is FrostyTabItem tabItemTarget) ||
                !(e.Data.GetData(typeof(FrostyTabItem)) is FrostyTabItem tabItemSource) ||
                tabItemTarget.Equals(tabItemSource) || !(tabItemTarget.Parent is FrostyTabControl control))
            {
                return;
            }
            
            int targetIndex = control.Items.IndexOf(tabItemTarget);

            control.Items.Remove(tabItemSource);
            control.Items.Insert(targetIndex, tabItemSource);
            tabItemSource.IsSelected = true;
        }

        private void aboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow win = new AboutWindow();
            win.ShowDialog();
        }

        private void RestoreMadden_Click(object sender, RoutedEventArgs e)
        {
            string pathFile = "selectedFolder.txt";
            string selectedFolder = "";

            // Check if a saved folder location exists
            if (File.Exists(pathFile))
            {
                selectedFolder = File.ReadAllText(pathFile);

                // Verify if the saved folder location is valid
                if (!Directory.Exists(selectedFolder))
                {
                    selectedFolder = ""; // Reset the selected folder if it's not valid
                }
            }

            // Prompt the user to select the game folder if one doesn't exist or the saved folder location can't be found
            if (string.IsNullOrEmpty(selectedFolder))
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.ValidateNames = false;
                openFileDialog.CheckFileExists = false;
                openFileDialog.CheckPathExists = true;
                openFileDialog.FileName = "Folder Selection.";

                if (openFileDialog.ShowDialog() == true)
                {
                    selectedFolder = Path.GetDirectoryName(openFileDialog.FileName);

                    // Save the selected folder location to a text file
                    File.WriteAllText(pathFile, selectedFolder);
                }
            }

            if (!string.IsNullOrEmpty(selectedFolder))
            {
                // Look for the specified folder and files and delete them if found
                string[] itemsToDelete = { "ModData", "tmp", "CryptBase.dll", "Madden24.old", "dpapi.dll", "datapath", "datapath.old" };
                bool foundAndDeleted = false;
                foreach (string item in itemsToDelete)
                {
                    string itemPath = Path.Combine(selectedFolder, item);
                    if (Directory.Exists(itemPath) || File.Exists(itemPath))
                    {
                        if (item == "ModData")
                        {
                            Directory.Delete(itemPath, true);
                        }
                        else
                        {
                            File.Delete(itemPath);
                        }
                        foundAndDeleted = true;
                    }
                }

                // Check for EAAntiCheat.GameServiceLauncher_Orig.exe and perform required actions
                string origFileName = "EAAntiCheat.GameServiceLauncher_Orig.exe";
                string newFileName = "EAAntiCheat.GameServiceLauncher.exe";
                string origFilePath = Path.Combine(selectedFolder, origFileName);
                string newFilePath = Path.Combine(selectedFolder, newFileName);

                if (File.Exists(origFilePath))
                {
                    if (File.Exists(newFilePath))
                    {
                        File.Delete(newFilePath);
                    }
                    File.Move(origFilePath, newFilePath);
                }

                // Check for Madden24.orig.exe and perform required actions
                string origMaddenFileName = "Madden24.orig.exe";
                string newMaddenFileName = "Madden24.exe";
                string origMaddenFilePath = Path.Combine(selectedFolder, origMaddenFileName);
                string newMaddenFilePath = Path.Combine(selectedFolder, newMaddenFileName);

                if (File.Exists(origMaddenFilePath))
                {
                    if (File.Exists(newMaddenFilePath))
                    {
                        File.Delete(newMaddenFilePath);
                    }
                    File.Move(origMaddenFilePath, newMaddenFilePath);
                }

                // Display a message based on whether the specified items were found and deleted or not
                string message = foundAndDeleted ? "Madden NFL 24 has been restored to Base Game." : "Madden NFL 24 is already Factory Version.";
                MessageBox.Show(message);
            }
            else
            {
                MessageBox.Show("Game folder selection was canceled or invalid. Please try again.");
            }
        }


        private void FrostyWindow_Closing(object sender, CancelEventArgs e)
        {
            if (m_project.IsDirty)
            {
                MessageBoxResult result = FrostyMessageBox.Show("Do you wish to save changes to " + m_project.DisplayName + "?", "Frosty Editor", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    if (SaveProject(false))
                    {
                        FrostyTaskWindow.Show("Saving Project", "", (task) => m_project.Save());
                        App.Logger.Log("Project saved to {0}", m_project.Filename);
                    }
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    // user hit the close button, so presumably wants to cancel the request
                    e.Cancel = true;
                    return;
                }
            }

            if (ProfilesLibrary.HasLoadedProfile)
            {
                Bookmarks.BookmarkDb.SaveDb();
            }
        }

        private void dataExplorer_SelectedAssetDoubleClick(object sender, RoutedEventArgs e)
        {
            AssetEntry entry = m_currentExplorer.SelectedAsset;
            OpenAsset(entry, m_currentExplorer == dataExplorer);
        }

        private void dataExplorer_SelectionChanged(object sender, RoutedEventArgs e)
        {
            EbxAssetEntry entry = dataExplorer.SelectedAsset as EbxAssetEntry;
            App.SelectedAsset = entry;
        }

        #region -- Context Menu Callbacks --

        // context menu callbacks

        private void contextMenuOpen_Click(object sender, RoutedEventArgs e)
        {
            foreach (var asset in m_currentExplorer.SelectedAssets)
            {
                OpenAsset(asset, m_currentExplorer == dataExplorer);
            }
        }

        private void contextMenuOpenOriginal_Click(object sender, RoutedEventArgs e)
        {
            foreach (var asset in m_currentExplorer.SelectedAssets)
            {
                OpenAsset(asset, m_currentExplorer == dataExplorer, true);
            }
        }

        private void contextMenuOpenOriginal_Loaded(object sender, RoutedEventArgs e)
        {
            var selectionHasModifiedEntries = m_currentExplorer.SelectedAssets.ToList().Find(a => a.IsModified) != null;
            ((MenuItem)sender).Visibility = selectionHasModifiedEntries ? Visibility.Visible : Visibility.Collapsed;
        }

        private void contextMenuRevert_Click(object sender, RoutedEventArgs e)
        {
            foreach (var asset in m_currentExplorer.SelectedAssets)
            {
                if (!asset.IsModified)
                    continue;

                for (int i = 1; i < TabControl.Items.Count; i++)
                {
                    FrostyTabItem tabItem = TabControl.Items[i] as FrostyTabItem;
                    if (tabItem.TabId == asset.Name)
                    {
                        RemoveTab(tabItem);
                        break;
                    }
                }

                FrostyTaskWindow.Show("Reverting Asset", "", (task) => { App.AssetManager.RevertAsset(asset, suppressOnModify: false); });

                dataExplorer.RefreshAll();
                legacyExplorer.RefreshAll();
            }
        }
        private void contextMenuImportAsset_Click(object sender, RoutedEventArgs e)
        {
            IList<LegacyFileEntry> assets = legacyExplorer.SelectedAssets.Cast<LegacyFileEntry>().ToList();

            if (assets.Count == 0)
            {
                App.Logger.Log("[Core] No assets selected for import.");
                return;
            }

            // Create a filter string based on all selected asset types.
            string filter = string.Join(";", assets.Select(a => "*." + a.Type));
            string description = string.Join(", ", assets.Select(a => "*." + a.Type)) + " (Legacy Files)";

            FrostyOpenFileDialog ofd = new FrostyOpenFileDialog("Open Legacy file", description + "|" + filter, "FifaLegacy")
            {
                Multiselect = assets.Count > 1 // Allow multiple file selection only if more than one asset is selected
            };

            if (ofd.ShowDialog())
            {
                Dictionary<string, LegacyFileEntry> assetDict = assets.ToDictionary(a => Path.GetFileNameWithoutExtension(a.Name).ToLower() + "." + a.Type.ToLower(), a => a);

                foreach (var fileName in ofd.FileNames)
                {
                    string fileKey = Path.GetFileNameWithoutExtension(fileName).ToLower() + "." + Path.GetExtension(fileName).TrimStart('.').ToLower();

                    // If there's only one asset, skip the name matching
                    if (assets.Count == 1 || assetDict.ContainsKey(fileKey))
                    {
                        LegacyFileEntry asset = assets.Count == 1 ? assets[0] : assetDict[fileKey];

                        App.Logger.Log("[Core] Attempting to import file: {0}", fileName);

                        FrostyTaskWindow.Show("Importing Legacy Asset", "", (task) =>
                        {
                            byte[] buffer;
                            using (NativeReader reader = new NativeReader(new FileStream(fileName, FileMode.Open, FileAccess.Read)))
                                buffer = reader.ReadToEnd();

                            App.AssetManager.ModifyCustomAsset("legacy", asset.Name, buffer);
                        });

                        App.Logger.Log("{0} imported to {1}", fileName, asset.Name);
                    }
                    else
                    {
                        App.Logger.Log("[Core] No matching asset found for file: {0}", fileName);
                    }
                }
                legacyExplorer.RefreshItems();
            }
        }

        private void contextMenuExportAsset_Click(object sender, RoutedEventArgs e)
        {
            if (legacyExplorer.SelectedAssets.Count == 0)
                return;

            IList<AssetEntry> assets = legacyExplorer.SelectedAssets;
            string saveFolderPath = "";

            if (assets.Count == 1) // Just one asset selected
            {
                LegacyFileEntry selectedAsset = (LegacyFileEntry)assets[0];
                Microsoft.Win32.SaveFileDialog sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Save Legacy File",
                    Filter = "*." + selectedAsset.Type + " (Legacy Files)|*." + selectedAsset.Type,
                    FileName = selectedAsset.Filename
                };

                if (sfd.ShowDialog() == true)
                {
                    saveFolderPath = new FileInfo(sfd.FileName).DirectoryName;
                }
            }
            else // Multiple assets selected
            {
                Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select a Folder",
                    CheckFileExists = false,
                    FileName = "[Select Folder]",
                    ValidateNames = false
                };

                if (ofd.ShowDialog() == true)
                {
                    saveFolderPath = System.IO.Path.GetDirectoryName(ofd.FileName);
                }
            }

            if (!string.IsNullOrEmpty(saveFolderPath))
            {
                FrostyTaskWindow.Show("Exporting Legacy Assets", "", (task) =>
                {
                    App.AssetManager.SendManagerCommand("legacy", "SetCacheModeEnabled", true);

                    int progress = 0;
                    foreach (LegacyFileEntry asset in assets)
                    {
                        task.Update(asset.Name, (progress / (double)assets.Count) * 100.0);
                        progress++;

                        string outFileName = saveFolderPath + "\\" + asset.Filename + "." + asset.Type;
                        using (NativeWriter writer = new NativeWriter(new FileStream(outFileName, FileMode.Create)))
                            writer.Write(new NativeReader(App.AssetManager.GetCustomAsset("legacy", asset)).ReadToEnd());
                    }

                    App.Logger.Log("Legacy files saved to {0}", saveFolderPath);
                    App.AssetManager.SendManagerCommand("legacy", "FlushCache");
                });
            }
        }

        private void contextMenuImportEbx_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Logger.Log("Starting Ebx Import...");

                IList<AssetEntry> assets = dataExplorer.SelectedAssets;
                if (assets.Count == 0)
                {
                    App.Logger.Log("No assets selected.");
                    return;
                }

                // Log the selected assets for debugging
                foreach (var asset in assets)
                {
                    App.Logger.Log($"Selected asset: {asset.Name}");
                }

                EbxAssetEntry firstEntry = assets[0] as EbxAssetEntry;
                AssetDefinition assetDefinition = App.PluginManager.GetAssetDefinition(firstEntry.Type) ?? new AssetDefinition();

                List<AssetImportType> filters = new List<AssetImportType>();
                assetDefinition.GetSupportedImportTypes(filters);

                StringBuilder filterStringBuilder = new StringBuilder();
                foreach (AssetImportType filter in filters)
                    filterStringBuilder.Append("|").Append(filter.FilterString);
                string filterString = filterStringBuilder.ToString().TrimStart('|');

                FrostyOpenFileDialog ofd = new FrostyOpenFileDialog("Import Asset", filterString, assetDefinition.GetType().Name)
                {
                    Multiselect = true
                };
                if (ofd.ShowDialog())
                {
                    App.Logger.Log("File dialog opened successfully.");
                    bool singleFileImport = ofd.FileNames.Length == 1; // Check if it's a single file import

                    foreach (var fileName in ofd.FileNames) // Replaced Parallel.ForEach with a standard loop
                    {
                        App.Logger.Log($"Attempting to import file: {fileName}");
                        string assetName = Path.GetFileNameWithoutExtension(fileName);
                        EbxAssetEntry matchingAsset = singleFileImport ? firstEntry : assets.FirstOrDefault(a => a.Name.EndsWith(assetName, StringComparison.OrdinalIgnoreCase)) as EbxAssetEntry;

                        if (matchingAsset != null && assetDefinition.Import(matchingAsset, fileName, filters[ofd.FilterIndex - 1].Extension))
                        {
                            // Refresh on UI thread if necessary
                            Application.Current.Dispatcher.Invoke(() => dataExplorer.RefreshItems());
                            App.Logger.Log("Imported {0} into {1}", fileName, matchingAsset.Name);
                        }
                        else
                        {
                            App.Logger.Log($"Failed to import {fileName} into {matchingAsset?.Name ?? "null"}");
                        }
                    }
                }
                else
                {
                    App.Logger.Log("File dialog was cancelled or failed to open.");
                }
            }
            catch (Exception ex)
            {
                App.Logger.LogError($"Error during Ebx import: {ex.Message}");
            }
        }

        private void contextMenuExportEbx_Click(object sender, RoutedEventArgs e)
        {
            IList<AssetEntry> assets = dataExplorer.SelectedAssets;
            if (assets.Count == 0)
                return;

            EbxAssetEntry entry = (EbxAssetEntry)assets[0];
            AssetDefinition assetDefinition = App.PluginManager.GetAssetDefinition(entry.Type) ?? new AssetDefinition();

            List<AssetExportType> filters = new List<AssetExportType>();
            assetDefinition.GetSupportedExportTypes(filters);

            StringBuilder filterStringBuilder = new StringBuilder();
            foreach (AssetExportType filter in filters)
                filterStringBuilder.Append("|").Append(filter.FilterString);
            string filterString = filterStringBuilder.ToString().TrimStart('|');


            FrostySaveFileDialog sfd = new FrostySaveFileDialog("Export Asset", filterString, assetDefinition.GetType().Name, entry.Filename);
            if (sfd.ShowDialog())
            {
                string directoryPath = Path.GetDirectoryName(sfd.FileName);
                foreach (EbxAssetEntry asset in assets)
                {
                    string extension = filters[sfd.FilterIndex - 1].Extension.StartsWith(".") ? filters[sfd.FilterIndex - 1].Extension : "." + filters[sfd.FilterIndex - 1].Extension;
                    string assetFileName = Path.Combine(directoryPath, asset.Filename + extension);

                    if (assetDefinition.Export(asset, assetFileName, filters[sfd.FilterIndex - 1].Extension))
                    {
                        App.Logger.Log("Exported {0} to {1}", asset.Name, assetFileName);
                    }
                }
            }
        }

        private void contextMenuBookmarkItemOpen_Click(object sender, RoutedEventArgs e)
        {
            if (BookmarkTreeView.SelectedItem == null)
                return;
            Bookmarks.BookmarkItem target = BookmarkTreeView.SelectedItem as Bookmarks.BookmarkItem;
            target.Target.NavigateTo(true);
        }

        private void contextMenuBookmarkItemFind_Click(object sender, RoutedEventArgs e)
        {
            if (BookmarkTreeView.SelectedItem == null)
                return;
            Bookmarks.BookmarkItem target = BookmarkTreeView.SelectedItem as Bookmarks.BookmarkItem;
            target.Target.NavigateTo(false);
        }

#endregion

        #region -- Bookmarks --

        private void BookmarkTabItem_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateBookmarkFilters();
        }

        private void BookmarkDb_ContextChanged(object sender, EventArgs e)
        {
            if (Bookmarks.BookmarkDb.CurrentContext != null)
            {
                Bookmarks.BookmarkDb.CurrentContext.TargetAvailable -= CurrentBookmarkContext_TargetAvailable;
                Bookmarks.BookmarkDb.CurrentContext.TargetUnavailable -= CurrentBookmarkContext_TargetUnavailable;
            }
            Bookmarks.BookmarkContext newcxt = sender as Bookmarks.BookmarkContext;
            if (newcxt != null)
            {
                newcxt.TargetAvailable += CurrentBookmarkContext_TargetAvailable;
                newcxt.TargetUnavailable += CurrentBookmarkContext_TargetUnavailable;
            }
            BookmarkContextPicker.SelectedItem = newcxt;
        }

        private void CurrentBookmarkContext_TargetAvailable(object sender, EventArgs e)
        {
            BookmarkAddButton.IsEnabled = true;
        }

        private void CurrentBookmarkContext_TargetUnavailable(object sender, EventArgs e)
        {
            BookmarkAddButton.IsEnabled = false;
        }

        private void BookmarkContextPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Bookmarks.BookmarkDb.CurrentContext = e.AddedItems[0] as Bookmarks.BookmarkContext;
        }

        private void AddSelectedBookmark()
        {
            if (Bookmarks.BookmarkDb.CurrentContext?.AvailableTarget != null)
            {
                Bookmarks.BookmarkTarget target = Bookmarks.BookmarkDb.CurrentContext.AvailableTarget;

                Bookmarks.BookmarkItem newBookmark;
                if (BookmarkTreeView.SelectedItem != null)
                {
                    Bookmarks.BookmarkItem parent = BookmarkTreeView.SelectedItem as Bookmarks.BookmarkItem;
                    // Abort if the selected parent is not a folder
                    if (parent != null && !(parent.Target is Bookmarks.FolderBookmarkTarget))
                    {
                        // Add as a sibling of the selected item
                        parent = parent.Parent;
                        if (parent == null)
                        {
                            newBookmark = new Bookmarks.BookmarkItem(target, null);
                            Bookmarks.BookmarkDb.CurrentContext?.Bookmarks.Add(newBookmark);
                        }
                        else
                        {
                            newBookmark = new Bookmarks.BookmarkItem(target, parent) {Parent = parent};
                            parent.Children.Add(newBookmark);
                            parent.IsExpanded = true;
                        }
                    }
                    else
                    {
                        newBookmark = new Bookmarks.BookmarkItem(target, parent) {Parent = parent};
                        if (parent != null)
                        {
                            parent.Children.Add(newBookmark);
                            parent.IsExpanded = true;
                        }
                    }
                }
                else
                {
                    newBookmark = new Bookmarks.BookmarkItem(target, null);
                    Bookmarks.BookmarkDb.CurrentContext?.Bookmarks.Add(newBookmark);
                }
                TriggerPulseAnimation();
            }
            UpdateBookmarkFilters();
            Bookmarks.BookmarkDb.SaveDb();
        }

        private void RemoveSelectedBookmark()
        {
            if (BookmarkTreeView.SelectedItem != null)
            {
                Bookmarks.BookmarkItem selected = (Bookmarks.BookmarkItem)BookmarkTreeView.SelectedItem;
                if (selected.Parent != null)
                {
                    selected.Parent.Children.Remove(selected);
                }
                else
                {
                    Bookmarks.BookmarkDb.CurrentContext.Bookmarks.Remove(selected);
                }
                UpdateBookmarkFilters();
                Bookmarks.BookmarkDb.SaveDb();
            }
        }

        private void AddBookmarkFolder()
        {
            Bookmarks.BookmarkItem parent = BookmarkTreeView.SelectedItem as Bookmarks.BookmarkItem;
            Bookmarks.BookmarkItem newItem = new Bookmarks.BookmarkItem(new Bookmarks.FolderBookmarkTarget(), null) {Name = "New Folder"};

            if (parent != null)
            {
                if (parent.Target is Bookmarks.FolderBookmarkTarget)
                {
                    parent.Children.Add(newItem);
                    newItem.Parent = parent;
                }
                else
                {
                    if (parent.Parent != null)
                    {
                        parent.Parent.Children.Add(newItem);
                        newItem.Parent = parent.Parent;
                    }
                    else
                        Bookmarks.BookmarkDb.CurrentContext.Bookmarks.Add(newItem);
                }
            }
            else
                Bookmarks.BookmarkDb.CurrentContext.Bookmarks.Add(newItem);

            TriggerPulseAnimation();
            UpdateBookmarkFilters();
            Bookmarks.BookmarkDb.SaveDb();
        }

        private void BookmarkAddButton_Click(object sender, RoutedEventArgs e)
        {
            AddSelectedBookmark();
        }

        private void BookmarkRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            RemoveSelectedBookmark();
        }

        private void BookmarkAddFolderButton_Click(object sender, RoutedEventArgs e)
        {
            AddBookmarkFolder();
        }

        private void BookmarkEditLabelButton_Click(object sender, RoutedEventArgs e)
        {
            Stack<Bookmarks.BookmarkItem> stack = new Stack<Bookmarks.BookmarkItem>();

            if (!(BookmarkTreeView.SelectedItem is Bookmarks.BookmarkItem item))
                return;

            stack.Push(item);
            // Walk up the tree
            while (item.Parent != null)
            {
                stack.Push(item.Parent);
                item = item.Parent;
            }


            ItemContainerGenerator gen = BookmarkTreeView.ItemContainerGenerator;
            while (stack.Count > 1)
            {
                gen = (gen.ContainerFromItem(stack.Pop()) as TreeViewItem).ItemContainerGenerator;
            }

            FrostyEditableLabel label = FindVisualChild<FrostyEditableLabel>(gen.ContainerFromItem(stack.Pop()));
            label.Text = label.Text ?? "";
            label.BeginEdit();
        }
        private HashSet<TreeViewItem> selectedItems = new HashSet<TreeViewItem>();

        private void BookmarkTreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem item = sender as TreeViewItem;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (item != null)
                {
                    if (this.selectedItems.Contains(item))
                    {
                        // Deselect item
                        item.IsSelected = false;
                        this.selectedItems.Remove(item);
                    }
                    else
                    {
                        // Select item
                        item.IsSelected = true;
                        this.selectedItems.Add(item);
                    }
                }
            }
            else
            {
                // If control is not being held down, clear the list and select the clicked item
                foreach (TreeViewItem selectedItem in this.selectedItems)
                {
                    selectedItem.IsSelected = false;
                }
                this.selectedItems.Clear();

                if (item != null)
                {
                    item.IsSelected = true;
                    this.selectedItems.Add(item);
                }
            }
            e.Handled = true;
        }

        private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeView && e.OriginalSource is FrameworkElement fe && fe.DataContext is TreeViewItem)
            {
                return;
            }

            foreach (TreeViewItem selectedItem in this.selectedItems)
            {
                selectedItem.IsSelected = false;
            }
            this.selectedItems.Clear();
        }

        private void BookmarkTreeView_MouseDoubleClick()
        {
            if (BookmarkTreeView.SelectedItem != null)
            {
                Bookmarks.BookmarkItem target = BookmarkTreeView.SelectedItem as Bookmarks.BookmarkItem;
                target.Target.NavigateTo(true);
            }
        }

        private void BookmarkTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            BookmarkRemoveButton.IsEnabled = BookmarkEditLabelButton.IsEnabled = BookmarkTreeView.SelectedItem != null;
        }

        private bool BookmarkFilter(Bookmarks.BookmarkItem target)
        {
            return string.IsNullOrEmpty(BookmarkFilterTextBox.Text) || target.Text.ToLower().Contains(BookmarkFilterTextBox.Text.ToLower()) || (target.Name?.ToLower().Contains(BookmarkFilterTextBox.Text.ToLower()) ?? false);
        }

        private void BookmarkFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateBookmarkFilters();
        }

        private void ApplyBookmarkFilter(Bookmarks.BookmarkItem item)
        {
            bool self = false; // Track whether any children are visible
            foreach (Bookmarks.BookmarkItem child in item.Children)
            {
                ApplyBookmarkFilter(child);
                self = self || child.IsVisible;
            }

            bool filter = BookmarkFilter(item);
            item.IsVisible = self || filter;
            item.IsFiltered = !filter;
        }

        private int CountBookmarks(Bookmarks.BookmarkItem item, bool visibleOnly)
        {
            if (visibleOnly && !item.IsVisible)
                return 0;

            int result = 0;
            foreach (Bookmarks.BookmarkItem child in item.Children)
                result += CountBookmarks(child, visibleOnly);

            return result + (visibleOnly ? BookmarkFilter(item) ? 1 : 0 : 1);
        }

        private bool refreshing; // For some reason, BookmarkTreeView.Items.Refresh causes recursion here...
        private void UpdateBookmarkFilters()
        {
            if (refreshing)
                return;
            int visible = 0;
            int total = 0;
            if (Bookmarks.BookmarkDb.CurrentContext != null)
            {
                foreach (Bookmarks.BookmarkItem item in Bookmarks.BookmarkDb.CurrentContext.Bookmarks)
                {
                    ApplyBookmarkFilter(item);
                    total += CountBookmarks(item, false);
                    visible += CountBookmarks(item, true);
                }
            }
            refreshing = true;
            BookmarkTreeView.Items.Refresh();
            refreshing = false;

            if (string.IsNullOrEmpty(BookmarkFilterTextBox.Text))
            {
                BookmarkFilterInfo.Content = "(no filter)";
            }
            else
            {
                BookmarkFilterInfo.Content = "(" + visible + " / " + total + ")";
            }
        }

        private void FrostyEditableLabel_EditEnded(object sender, EventArgs e)
        {
            UpdateBookmarkFilters();
            Bookmarks.BookmarkDb.SaveDb();
            Focus();
        }

        private static readonly SolidColorBrush NormalBrush = new SolidColorBrush(new Color { R = 140, G = 140, B = 140, A = 255 });
        private void TriggerPulseAnimation()
        {
            BookmarkTabItem.Background = NormalBrush;
            DoubleAnimation lastFadeAnimation = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromSeconds(1.0))) {FillBehavior = FillBehavior.HoldEnd};
            NormalBrush.BeginAnimation(Brush.OpacityProperty, lastFadeAnimation);
        }

        #endregion

        private void optionsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OptionsWindow win = new OptionsWindow();
            win.ShowDialog();
        }

        private void MiscTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private static T FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T dObj)
                    {
                        return dObj;
                    }

                    T childItem = FindVisualChild<T>(child);
                    if (childItem != null) return childItem;
                }
            }
            return null;
        }

        private void closeSoloDocumentMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Get the currently selected tab from the TabControl
            FrostyTabItem selectedTab = TabControl.SelectedItem as FrostyTabItem;

            // Check if a tab is selected
            if (selectedTab != null)
            {
                // Close the selected tab
                RemoveTab(selectedTab);
            }
        }

        private void closeAllDocumentsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RemoveAllTabs();
        }

        private void BookmarkTreeView_MouseDown(object sender, MouseButtonEventArgs e) {
            TreeViewItem treeItem = (TreeViewItem)BookmarkTreeView.ItemContainerGenerator.ContainerFromItem(BookmarkTreeView.SelectedItem);
            if (treeItem != null) treeItem.IsSelected = false;
        }

        private void OpenColorizer_Click(object sender, RoutedEventArgs e)
        {
            FrostbiteColorizer win = new FrostbiteColorizer();

            win.Show();
        }
        private void contextMenuCopyName_Click(object sender, RoutedEventArgs e)
        {
            var selectedAssets = m_currentExplorer.SelectedAssets;
            if (selectedAssets != null && selectedAssets.Count > 0)
            {
                StringBuilder names = new StringBuilder();
                foreach (var asset in selectedAssets)
                {
                    string fileName = asset.DisplayName;

                    // Check if the selected asset is a legacy file and append the file extension
                    if (asset is LegacyFileEntry legacyAsset)
                    {
                        fileName += "." + legacyAsset.Type.ToLower();
                    }

                    names.AppendLine(fileName);
                }
                Clipboard.SetText(names.ToString().TrimEnd());
            }
        }
        private void contextMenuCopyPath_Click(object sender, RoutedEventArgs e)
        {
            var selectedAssets = m_currentExplorer.SelectedAssets;
            if (selectedAssets != null && selectedAssets.Count > 0)
            {
                StringBuilder paths = new StringBuilder();
                foreach (var asset in selectedAssets)
                {
                    string fileName = asset.DisplayName;

                    // Check if the selected asset is a legacy file and append the file extension
                    if (asset is LegacyFileEntry legacyAsset)
                    {
                        fileName += "." + legacyAsset.Type.ToLower();
                    }

                    string fullPathWithFileName = System.IO.Path.Combine(asset.Path, fileName);
                    fullPathWithFileName = fullPathWithFileName.Replace('\\', '/');
                    paths.AppendLine(fullPathWithFileName);
                }
                Clipboard.SetText(paths.ToString().TrimEnd());
            }
        }
    }
}
