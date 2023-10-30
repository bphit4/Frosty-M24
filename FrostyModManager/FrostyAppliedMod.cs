﻿using Frosty.Core.Mod;
using System.Windows.Media;

namespace FrostyModManager
{
    public class FrostyAppliedMod
    {
        public string ModName
        {
            get
            {
                if (Mod != null)
                    return Mod.ModDetails.Title;
                else
                    return BackupFileName;
            }
        }
        public ImageSource ModIcon
        {
            get
            {
                if (Mod != null)
                    return Mod.ModDetails.Icon;
                else
                    return new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyModManager;component/Images/ModImportWarning.png") as ImageSource;
            }
        }

        public bool IsEnabled { get; set; }
        public bool IsFound { get; set; }
        public IFrostyMod Mod { get; }

        public string BackupFileName { get; }
        public int PriorityOrder { get; internal set; }

        public FrostyAppliedMod(IFrostyMod inMod, bool inIsEnabled = true)
        {
            Mod = inMod;
            IsEnabled = inIsEnabled;
            IsFound = true;
        }

        public FrostyAppliedMod(string inBackupFileName, bool inIsEnabled = true)
        {
            BackupFileName = inBackupFileName;
            IsEnabled = inIsEnabled;
            IsFound = false;
        }
        public int HighestPriority { get; internal set; }  // Add this line

        public string PriorityOrderText {
            get {
                if (PriorityOrder == 1)
                {
                    return "Highest Priority  ";
                }
                else if (PriorityOrder == HighestPriority)
                {
                    return "Lowest Priority  ";
                }
                else
                {
                    return "";
                }
            }
        }

    }
}
