﻿using FrostySdk.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Frosty.Core.Controls;
using System.Windows.Media;
using FrostySdk;
using FrostySdk.Attributes;
using System.IO;
using System.Text;

namespace TypeExplorerPlugin
{
    [TemplatePart(Name = PART_TypesListBox, Type = typeof(ListBox))]
    [TemplatePart(Name = PART_TypeFilter, Type = typeof(TextBox))]
    [TemplatePart(Name = PART_TypeFieldsTextBox, Type = typeof(RichTextBox))]
    [TemplatePart(Name = PART_HideEmptyCheckBox, Type = typeof(CheckBox))]
    public class FrostyTypeExplorer : FrostyBaseEditor
    {
        public override ImageSource Icon => TypeExplorerMenuExtension.imageSource;

        public List<TypeItem> TypeItems { get; private set; }

        private const string PART_TypesListBox = "PART_TypesListBox";
        private const string PART_TypeFilter = "PART_TypeFilter";
        private const string PART_TypeFieldsTextBox = "PART_TypeFieldsTextBox";
        private const string PART_HideEmptyCheckBox = "PART_HideEmptyCheckBox";

        private ListBox typesListBox;
        private TextBox typeFilterTextBox;
        private RichTextBox typeFieldsTextBox;
        private CheckBox hideEmptyCheckBox;

        private static readonly SolidColorBrush TextColor = new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xDC));
        private static readonly SolidColorBrush EnumColor = new SolidColorBrush(Color.FromRgb(0xB8, 0xD7, 0xA3));
        private static readonly SolidColorBrush ClassColor = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0));
        private static readonly SolidColorBrush StructColor = new SolidColorBrush(Color.FromRgb(0x86, 0xC6, 0x91));
        private static readonly SolidColorBrush LiteralColor = new SolidColorBrush(Color.FromRgb(0xB5, 0xCE, 0xA8));

        private ILogger logger;
        private Button exportToCsButton;
        private Button batchExportAllToCsButton;

        public class TypeItem
        {
            public Type Type { get; }
            public string Name { get; }
            public SolidColorBrush Brush { get; }

            public TypeItem(Type type)
            {
                Type = type;
                Name = type.Name;
                Brush = GetTypeColor(type);
            }
        }

        static FrostyTypeExplorer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FrostyTypeExplorer), new FrameworkPropertyMetadata(typeof(FrostyTypeExplorer)));
        }

        public FrostyTypeExplorer(ILogger inLogger = null)
        {
            logger = inLogger;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            typesListBox = GetTemplateChild(PART_TypesListBox) as ListBox;
            typeFilterTextBox = GetTemplateChild(PART_TypeFilter) as TextBox;
            typeFieldsTextBox = GetTemplateChild(PART_TypeFieldsTextBox) as RichTextBox;
            hideEmptyCheckBox = GetTemplateChild(PART_HideEmptyCheckBox) as CheckBox;
            exportToCsButton = GetTemplateChild("ExportToCsButton") as Button;
            batchExportAllToCsButton = GetTemplateChild("BatchExportAllToCsButton") as Button;

            Loaded += FrostyTypeExplorer_Loaded;
            typesListBox.SelectionChanged += TypesListBox_SelectionChanged;
            typeFilterTextBox.LostFocus += TypeFilterTextBox_LostFocus;
            typeFilterTextBox.KeyUp += TypeFilterTextBox_KeyUp;
            hideEmptyCheckBox.Checked += HideEmptyCheckBox_Checked;
            hideEmptyCheckBox.Unchecked += HideEmptyCheckBox_Unchecked;
            exportToCsButton.Click += ExportToCsButton_Click;
            batchExportAllToCsButton.Click += BatchExportAllToCsButton_Click;

            // Set up right-click menu
            typesListBox.MouseRightButtonUp += TypesListBox_MouseRightButtonUp;

            typesListBox.SelectionMode = SelectionMode.Extended;
        }

        private void TypeFilterTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                TypeFilterTextBox_LostFocus(this, new RoutedEventArgs());
        }

        private void TypeFilterTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(typeFilterTextBox.Text))
            {
                if (hideEmptyCheckBox.IsChecked == true)
                    typesListBox.Items.Filter = a => FilterItem(((TypeItem)a).Type);
                else
                    typesListBox.Items.Filter = a => ((TypeItem)a).Type.Name.ToLower().Contains(typeFilterTextBox.Text.ToLower()) || TypeLibrary.IsSubClassOf(((TypeItem)a).Type, typeFilterTextBox.Text);
            }
            else if (hideEmptyCheckBox.IsChecked == true)
                typesListBox.Items.Filter = a => GetPropertyCount(((TypeItem)a).Type) > 0;
            else
                typesListBox.Items.Filter = null;
        }

        private void TypesListBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (typesListBox.SelectedItem is TypeItem typeItem)
            {
                Type type = typeItem.Type;

                FlowDocument document = new FlowDocument();
                Paragraph paragraph = new Paragraph();

                paragraph.Inlines.Add(new Run(type.Name) { Foreground = GetTypeColor(type) });

                // enums
                if (type.IsEnum)
                {
                    paragraph.Inlines.Add(new Run("\n{\n") { Foreground = TextColor });

                    var underlyingType = Enum.GetUnderlyingType(type);
                    var values = type.GetEnumValues();

                    // enum members
                    for (int i = 0; i < values.Length; i++)
                    {
                        paragraph.Inlines.Add(new Run($"    {values.GetValue(i)} = ") { Foreground = TextColor });
                        paragraph.Inlines.Add(new Run(Convert.ChangeType(values.GetValue(i), underlyingType).ToString()) { Foreground = LiteralColor });
                        paragraph.Inlines.Add(new Run(i != values.Length - 1 ? ",\n" : "\n") { Foreground = TextColor });
                    }

                    paragraph.Inlines.Add(new Run("}") { Foreground = TextColor });
                    document.Blocks.Add(paragraph);

                    typeFieldsTextBox.Document = document;
                    return;
                }

                // base types
                if (type.BaseType != null && type.BaseType.Name != "Object")
                {
                    paragraph.Inlines.Add(new Run(" : ") { Foreground = TextColor });
                    paragraph.Inlines.Add(new Run(type.BaseType.Name) { Foreground = ClassColor });
                }

                paragraph.Inlines.Add(new Run("\n{\n") { Foreground = TextColor });

                var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

                // class properties
                foreach (var pi in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    if (pi.Name.StartsWith("_"))
                        continue;
                    
                    string propName = pi.PropertyType.Name;
                    paragraph.Inlines.Add(new Run("    "));

                    // use ebx field type names
                    if (propName == "Single")
                        propName = "Float32";
                    else if (propName == "Double")
                        propName = "Float64";

                    // get the generic/ref type
                    if (propName == "List`1" || propName == "PointerRef")
                    {
                        paragraph.Inlines.Add(new Run(propName == "List`1" ? "List" : "PointerRef") { Foreground = ClassColor });
                        paragraph.Inlines.Add(new Run("<") { Foreground = TextColor });

                        if (propName == "List`1")
                        {
                            Type listType = pi.PropertyType.GetGenericArguments()[0];

                            // list of pointerrefs
                            if (listType.Name == "PointerRef")
                            {
                                paragraph.Inlines.Add(new Run("PointerRef") { Foreground = ClassColor });
                                paragraph.Inlines.Add(new Run("<") { Foreground = TextColor });
                                paragraph.Inlines.Add(new Run(pi.GetCustomAttribute<EbxFieldMetaAttribute>().BaseType?.Name) { Foreground = ClassColor });
                                paragraph.Inlines.Add(new Run(">") { Foreground = TextColor });
                            }
                            else
                                paragraph.Inlines.Add(new Run(listType.Name) {Foreground = GetTypeColor(listType) });
                        }
                        else
                        {
                            paragraph.Inlines.Add(new Run(pi.GetCustomAttribute<EbxFieldMetaAttribute>().BaseType?.Name) {Foreground = ClassColor});
                        }

                        paragraph.Inlines.Add(new Run(">") { Foreground = TextColor });
                    }
                    else
                    {
                        paragraph.Inlines.Add(new Run(propName) { Foreground = GetTypeColor(pi.PropertyType) });
                    }

                    paragraph.Inlines.Add(new Run($" {pi.Name};\n") { Foreground = TextColor });
                }

                paragraph.Inlines.Add(new Run("}") { Foreground = TextColor });
                document.Blocks.Add(paragraph);
                typeFieldsTextBox.Document = document;

                ContextMenu contextMenu = new ContextMenu();

                if (typesListBox.SelectedItems.Count == 1)
                {
                    MenuItem exportItem = new MenuItem { Header = "Export to .cs" };
                    exportItem.Click += (s, args) => { ExportTypeToCs(typesListBox.SelectedItem as TypeItem); };
                    contextMenu.Items.Add(exportItem);
                }
                else if (typesListBox.SelectedItems.Count > 1)
                {
                    MenuItem batchExportItem = new MenuItem { Header = "Batch Export Selected to .cs" };
                    batchExportItem.Click += (s, args) => { BatchExportSelectedToCs(); };
                    contextMenu.Items.Add(batchExportItem);
                }

                typesListBox.ContextMenu = contextMenu;
            }

            else
            {
                typeFieldsTextBox.Document = new FlowDocument();
            }
        }

        private static SolidColorBrush GetTypeColor(Type type)
        {
            if (type.IsEnum)
                return EnumColor;
            
            if (type.IsValueType)
                return StructColor;

            return ClassColor;
        }

        private void HideEmptyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            typesListBox.Items.Filter = a => FilterItem(((TypeItem)a).Type);
        }

        private bool FilterItem(Type type)
        {
            if (type.IsEnum && type.Name.ToLower().Contains(typeFilterTextBox.Text.ToLower()))
                return true;

            if (type.Name.ToLower().Contains(typeFilterTextBox.Text.ToLower()) || TypeLibrary.IsSubClassOf(type, typeFilterTextBox.Text))
                return GetPropertyCount(type) > 0;

            return false;
        }

        private int GetPropertyCount(Type type)
        {
            List<PropertyInfo> props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).ToList();

            for (int i = 0; i < props.Count; i++)
                if (props[i].Name.StartsWith("_"))
                    props.Remove(props[i]);

            return props.Count;
        }

        private void HideEmptyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(typeFilterTextBox.Text))
                typesListBox.Items.Filter = a => ((TypeItem)a).Type.Name.ToLower().Contains(typeFilterTextBox.Text.ToLower()) || TypeLibrary.IsSubClassOf(((TypeItem)a).Type, typeFilterTextBox.Text);
            else
                typesListBox.Items.Filter = null;
        }

        private void FrostyTypeExplorer_Loaded(object sender, RoutedEventArgs e)
        {
            TypeItems = new List<TypeItem>();

            foreach (Type type in TypeLibrary.GetConcreteTypes())
            {
                TypeItems.Add(new TypeItem(type));
            }

            typesListBox.ItemsSource = TypeItems;
            typesListBox.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("Name", System.ComponentModel.ListSortDirection.Ascending));
            typesListBox.Items.Refresh();
        }

        private void ExportTypeToCs(TypeItem typeItem)
        {
            if (typeItem == null) return;

            Type type = typeItem.Type;
            StringBuilder csCode = new StringBuilder();

            // Add class or enum definition
            if (type.IsEnum)
            {
                csCode.AppendLine($"{type.Name}");
                csCode.AppendLine("{");

                var values = Enum.GetValues(type);
                for (int i = 0; i < values.Length; i++)
                {
                    csCode.Append($"    {Enum.GetName(type, values.GetValue(i))} = {Convert.ToInt32(values.GetValue(i))}");
                    csCode.AppendLine(i != values.Length - 1 ? "," : "");
                }

                csCode.AppendLine("}");
            }
            else
            {
                csCode.Append($"{type.Name}");

                // Include base type if it exists and is not Object
                if (type.BaseType != null && type.BaseType.Name != "Object")
                {
                    csCode.AppendLine($" : {type.BaseType.Name}");
                }
                else
                {
                    csCode.AppendLine();
                }

                csCode.AppendLine("{");

                foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    // Skip undesired properties
                    if (prop.Name.StartsWith("__")) continue;

                    // Convert System types to custom types
                    string propType = prop.PropertyType.Name;
                    if (propType == "String") propType = "CString";
                    else if (propType == "Single") propType = "Float32";

                    // Handle generic types
                    if (propType.StartsWith("List`"))
                    {
                        Type listType = prop.PropertyType.GetGenericArguments()[0];
                        if (listType.Name == "PointerRef")
                        {
                            propType = $"List<PointerRef<{prop.GetCustomAttribute<EbxFieldMetaAttribute>().BaseType?.Name}>>";
                        }
                        else
                        {
                            propType = $"List<{listType.Name}>";
                        }
                    }
                    else if (propType == "PointerRef")
                    {
                        propType = $"PointerRef<{prop.GetCustomAttribute<EbxFieldMetaAttribute>().BaseType?.Name}>";
                    }

                    csCode.AppendLine($"    {propType} {prop.Name};");
                }

                csCode.AppendLine("}");
            }

            // Check and create the directory if it doesn't exist
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Type_Exports");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Save to .cs file in the "Type_Exports" folder
            using (StreamWriter writer = new StreamWriter(Path.Combine(folderPath, $"{typeItem.Name}.cs")))
            {
                writer.Write(csCode.ToString());
            }
        }

        private void ExportToCsButton_Click(object sender, RoutedEventArgs e)
        {
            if (typesListBox.SelectedItems.Count == 1)
            {
                ExportTypeToCs(typesListBox.SelectedItem as TypeItem);
            }
            else if (typesListBox.SelectedItems.Count > 1)
            {
                BatchExportSelectedToCs();
            }
        }


        private void BatchExportAllToCsButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (TypeItem typeItem in TypeItems)
            {
                ExportTypeToCs(typeItem);
            }
        }

        private void BatchExportSelectedToCs()
        {
            // Log export initiation
            logger.Log("Batch export initiated...");

            int exportCount = 0;

            foreach (var selectedItem in typesListBox.SelectedItems)
            {
                if (selectedItem is TypeItem typeItem)
                {
                    ExportTypeToCs(typeItem);
                    exportCount++;
                }
            }

            // Log successful export
            logger.Log($"Batch export successful: {exportCount} files exported to some-folder-path");
        }

        private void TypesListBox_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ContextMenu contextMenu = new ContextMenu();

            if (typesListBox.SelectedItems.Count == 1)
            {
                MenuItem exportItem = new MenuItem { Header = "Export to .cs" };
                exportItem.Click += (s, args) => { ExportTypeToCs(typesListBox.SelectedItem as TypeItem); };
                contextMenu.Items.Add(exportItem);
            }
            else if (typesListBox.SelectedItems.Count > 1)
            {
                MenuItem batchExportItem = new MenuItem { Header = "Batch Export Selected to .cs" };
                batchExportItem.Click += (s, args) => { BatchExportSelectedToCs(); };
                contextMenu.Items.Add(batchExportItem);
            }

            typesListBox.ContextMenu = contextMenu;
            contextMenu.IsOpen = true;
        }
    }
}
