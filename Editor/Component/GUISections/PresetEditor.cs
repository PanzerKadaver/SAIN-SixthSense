﻿using EFT.Visual;
using SAIN.BotPresets;
using SAIN.Editor.Abstract;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static UnityEngine.GUILayout;
using static SAIN.Editor.Names;

namespace SAIN.Editor
{
    public class PresetEditor : EditorAbstract
    {
        public PresetEditor(GameObject obj) : base(obj)
        {
            List<string> sections = new List<string>();
            List<string> props = new List<string>();
            WildSpawnTypes = PresetManager.BotTypes;
            foreach (var type in WildSpawnTypes)
            {
                if (!sections.Contains(type.Section))
                {
                    sections.Add(type.Section);
                }
            }

            Sections = sections.ToArray();
        }

        public void PresetMenu()
        {
            if (ExpandPresetEditor = BuilderClass.ExpandableMenu("Bot Preset Editor", ExpandPresetEditor, "Edit Values for particular bot types and difficulty settings"))
            {
                FirstMenu();
                DifficultyTable();
                PropertyMenu();
                PresetEditWindow();
            }
        }

        private float Rect1OptionSpacing = 2f;
        private float Rect2OptionSpacing = 2f;
        private float SectionSelOptionHeight => (Rect12Height / Sections.Length) - (Rect1OptionSpacing);
        private float SectionRectX => BothRectGap;
        private float SectionRectWidth = 165f;
        private float BothRectGap = 6f;
        private float TypeRectWidth => RectLayout.MainWindow.width - SectionRectWidth - BothRectGap * 2f;
        private float TypeRectX => SectionRectX + SectionRectWidth + BothRectGap;
        private float Rect12Height = 285f;
        private float Rect12HeightMinusMargin => Rect12Height - ScrollMargin;

        private float ScrollMargin = 0f;

        private float TypeOptLabelHeight = 18f;
        private float TypeOptOptionHeight = 19f;
        private float SectOptFontSizeInc = 15f;

        private float YWithGenClose = 160f;
        private float YWithGenOpen = 270f;
        private float MenuStartHeight => Editor.ExpandGeneral ? YWithGenOpen : YWithGenClose;

        private Rect SectionRectangle;
        private Rect TypeRectangle;

        private Vector2 FirstMenuScroll = Vector2.zero;
        private void FirstMenu()
        {
            SectionRectangle = new Rect(SectionRectX, MenuStartHeight, SectionRectWidth, Rect12Height);
            BeginArea(SectionRectangle);
            SelectSection();
            EndArea();

            TypeRectangle = new Rect(TypeRectX, MenuStartHeight, TypeRectWidth, Rect12HeightMinusMargin);
            //Rect scrollRect = new Rect(ScrollMargin, ScrollMargin, TypeRectWidth - ScrollMargin * 2f, ScrollHeight);

            BeginArea(TypeRectangle);

            FirstMenuScroll = BeginScrollView(FirstMenuScroll);
            SelectType();
            EndScrollView();

            EndArea();

            Space(Rect12Height);

            OpenAdjustmentWindow = Toggle(OpenAdjustmentWindow, "Open GUI Adjustment");
        }

        private Rect RelativeRect( Rect mainRect , Rect insideRect, Rect lastRect)
        {
            float X = lastRect.x + insideRect.x + mainRect.x;
            float Y = lastRect.y + insideRect.y + mainRect.y;
            return new Rect(X, Y, lastRect.width, lastRect.height);
        }

        private Rect RelativeRectMainWindow(Rect insideRect, Rect lastRect)
        {
            return RelativeRect(RectLayout.MainWindow, insideRect, lastRect);
        }

        private Rect RelativeRectMainWindow(Rect lastRect)
        {
            float X = lastRect.x + RectLayout.MainWindow.x;
            float Y = lastRect.y + RectLayout.MainWindow.y;
            return new Rect(X, Y, lastRect.width, lastRect.height);
        }

        private Rect? RelativeRectLastRectMainWindow(Rect insideRect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return null;
            }
            return RelativeRectMainWindow(insideRect, GUILayoutUtility.GetLastRect());
        }

        public bool OpenAdjustmentWindow = false;
        private Rect AdjustmentRect => Editor.AdjustmentRect;

        public void GUIAdjustment(int wini)
        {
            GUI.DragWindow(new Rect(0, 0, AdjustmentRect.width, 20f));

            Space(25f);

            BeginVertical();

            YWithGenClose = HorizontalSliderNoStyle(nameof(YWithGenClose), YWithGenClose, 100f, 500f);
            YWithGenClose = Round(YWithGenClose);
            YWithGenOpen = HorizontalSliderNoStyle(nameof(YWithGenOpen), YWithGenOpen, 100f, 500f);
            YWithGenOpen = Round(YWithGenOpen);

            Space(10f);

            SectionRectWidth = HorizontalSliderNoStyle(nameof(SectionRectWidth), SectionRectWidth, 100f, 500f);
            SectionRectWidth = Round(SectionRectWidth);
            BothRectGap = HorizontalSliderNoStyle(nameof(BothRectGap), BothRectGap, 0f, 50f);
            BothRectGap = Round(BothRectGap);
            Rect12Height = HorizontalSliderNoStyle(nameof(Rect12Height), Rect12Height, 100f, 500f);
            Rect12Height = Round(Rect12Height);

            Space(10f);

            ScrollMargin = HorizontalSliderNoStyle(nameof(ScrollMargin), ScrollMargin, 0, 50f);
            ScrollMargin = Round(ScrollMargin);
            Rect1OptionSpacing = HorizontalSliderNoStyle(nameof(Rect1OptionSpacing), Rect1OptionSpacing, 0f, 50f);
            Rect1OptionSpacing = Round(Rect1OptionSpacing);
            Rect2OptionSpacing = HorizontalSliderNoStyle(nameof(Rect2OptionSpacing), Rect2OptionSpacing, 0, 50f);
            Rect2OptionSpacing = Round(Rect2OptionSpacing, 1);

            Space(10f);

            TypeOptLabelHeight = HorizontalSliderNoStyle(nameof(TypeOptLabelHeight), TypeOptLabelHeight, 5, 25f);
            TypeOptLabelHeight = Round(TypeOptLabelHeight, 1);
            TypeOptOptionHeight = HorizontalSliderNoStyle(nameof(TypeOptOptionHeight), TypeOptOptionHeight, 5, 50f);
            TypeOptOptionHeight = Round(TypeOptOptionHeight);
            SectOptFontSizeInc = HorizontalSliderNoStyle(nameof(SectOptFontSizeInc), SectOptFontSizeInc, 0, 50f);
            SectOptFontSizeInc = Round(SectOptFontSizeInc);

            Space(10f);

            CreateGUIAdjustmentSliders();

            EndVertical();
        }

        public float Round(float value, float dec = 10f)
        {
            return Mathf.Round(value * dec) / dec;
        }

        private void SelectSection()
        {
            BeginVertical();
            Space(Rect1OptionSpacing);

            for (int i = 0; i < Sections.Length; i++)
            {
                string section = Sections[i];
                bool selected = SelectedSections.Contains(section);

                GUIStyle style = new GUIStyle(GetStyle(StyleNames.list))
                {
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleLeft
                };

                if (selected)
                {
                    style.fontStyle = FontStyle.Bold;
                    int normalFontSize = style.fontSize;
                    int increase = Mathf.RoundToInt(SectOptFontSizeInc);
                    style.fontSize = normalFontSize + increase;
                }

                BeginHorizontal();
                Space(Rect1OptionSpacing);

                float width = SectionRectWidth - Rect1OptionSpacing * 2;
                if (Toggle(selected, section, style, Width(width), Height(SectionSelOptionHeight)))
                {
                    if (!selected)
                    {
                        SelectedSections.Add(section);
                    }
                }
                else
                {
                    if (selected)
                    {
                        SelectedSections.Remove(section);
                    }
                }
                if (MouseFunctions.CheckMouseDrag())
                {
                    if (!selected)
                    {
                        SelectedSections.Add(section);
                    }
                }

                Space(Rect1OptionSpacing);
                EndHorizontal();
            }

            Space(Rect1OptionSpacing);
            EndVertical();
        }

        private void SelectType()
        {
            for (int i = 0; i < SelectedSections.Count;i++) 
            {
                string section = SelectedSections[i];
                GUIStyle style = new GUIStyle(GetStyle(StyleNames.label))
                {
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleLeft
                };

                BeginHorizontal();
                Space(Rect2OptionSpacing);

                Label(section, style, Height(TypeOptLabelHeight), Width(200f));

                FlexibleSpace();
                EndHorizontal();

                Space(Rect2OptionSpacing);

                for (int j = 0; j < WildSpawnTypes.Length; j++)
                {
                    BotType type = WildSpawnTypes[j];
                    if (type.Section == section)
                    {
                        GUIStyle style2 = new GUIStyle(GetStyle(StyleNames.toggle))
                        {
                            fontStyle = FontStyle.Normal,
                            alignment = TextAnchor.LowerCenter
                        };
                        bool typeSelected = SelectedWildSpawnTypes.Contains(type);

                        BeginHorizontal();
                        Space(Rect2OptionSpacing);

                        bool AddToList = Toggle(typeSelected, type.Name, style2, Height(TypeOptOptionHeight));

                        Rect? lastRect = RelativeRectLastRectMainWindow(TypeRectangle);
                        if (lastRect != null && CheckDrag(lastRect.Value))
                        {
                            AddToList = !Editor.ShiftKeyPressed;
                        }

                        if (AddToList && !SelectedWildSpawnTypes.Contains(type))
                        {
                            SelectedWildSpawnTypes.Add(type);
                        }
                        else if (!AddToList && SelectedWildSpawnTypes.Contains(type))
                        {
                            SelectedWildSpawnTypes.Remove(type);
                        }

                        Space(Rect2OptionSpacing);
                        EndHorizontal();
                    }
                }
                Space(Rect2OptionSpacing);
            }
        }

        public readonly string[] Sections;
        private readonly List<string> SelectedSections = new List<string>();

        private readonly BotType[] WildSpawnTypes;
        private readonly List<BotType> SelectedWildSpawnTypes = new List<BotType>();

        private void DifficultyTable(int optionsPerLine = 2, float labelHeight = 35f, float labelWidth = 200f, float optionHeight = 25f)
        {
            OpenDifficultySelection = BuilderClass.ExpandableMenu("Difficulties", OpenDifficultySelection, "Select which difficulties you wish to modify.");
            if (!OpenDifficultySelection)
            {
                return;
            }
            bool oldValue = SelectAllDifficulties;
            SelectAllDifficulties = LabelAndAll(
                "Select Difficulty",
                SelectAllDifficulties,
                labelHeight,
                labelWidth);

            BeginHorizontal();

            int spacing = 0;
            for (int i = 0; i < BotDifficultyOptions.Length; i++)
            {
                var diff = BotDifficultyOptions[i];

                CheckAddAll(SelectAllDifficulties, oldValue, SelectedDifficulties, diff);

                spacing = SelectionGridOption(
                    spacing,
                    diff.ToString(),
                    SelectedDifficulties,
                    diff,
                    optionsPerLine,
                    optionHeight);
            }

            EndHorizontal();
        }

        private bool OpenDifficultySelection = false;
        public readonly BotDifficulty[] BotDifficultyOptions = { BotDifficulty.easy, BotDifficulty.normal, BotDifficulty.hard, BotDifficulty.impossible };
        private readonly List<BotDifficulty> SelectedDifficulties = new List<BotDifficulty>();
        private bool SelectAllDifficulties = false;

        private void PropertyMenu(int optionsPerLine = 3, float labelHeight = 35f, float labelWidth = 200f, float optionHeight = 25f, float scrollHeight = 200f)
        {
            OpenPropertySelection = BuilderClass.ExpandableMenu("Properties", OpenPropertySelection, "Select which properties you wish to modify.");
            if (!OpenPropertySelection)
            {
                return;
            }

            bool oldValue = SelectAllProperties;
            SelectAllProperties = LabelAndAll(
                "Select Properties",
                SelectAllProperties,
                labelHeight,
                labelWidth);

            PropertyScroll = BeginScrollView(PropertyScroll, Height(scrollHeight));

            BeginHorizontal();

            var properties = PresetManager.Properties;
            int spacing = 0;
            for (int i = 0; i < properties.Count; i++)
            {
                CheckAddAll(SelectAllProperties, oldValue, SelectedProperties, properties[i]);

                spacing = SelectionGridOption(
                    spacing,
                    properties[i].Name,
                    SelectedProperties,
                    properties[i],
                    optionsPerLine,
                    optionHeight);
            }

            EndHorizontal();
            EndScrollView();
        }

        private bool OpenPropertySelection = false;
        private readonly List<PropertyInfo> SelectedProperties = new List<PropertyInfo>();
        private bool SelectAllProperties = false;
        private static Vector2 PropertyScroll = Vector2.zero;
        private static bool ExpandPresetEditor = false;

        private void PresetEditWindow()
        {
            if (SelectedProperties.Count > 0 && SelectedWildSpawnTypes.Count > 0 && SelectedDifficulties.Count > 0)
            {
                BeginHorizontal();

                BotType typeInEdit = SelectedWildSpawnTypes[0];
                Box("Editing: " + SelectedWildSpawnTypes.Count + " Bots for difficulties: " + SelectedDifficulties, Height(35f));

                if (Button("Save", Height(35f), Width(200f)))
                {
                    SaveValues(typeInEdit);
                    return;
                }
                if (Button("Discard", Height(35f)))
                {
                    Reset();
                    return;
                }

                FlexibleSpace();
                EndHorizontal();

                for (int i = 0; i < SelectedProperties.Count; i++)
                {
                    CreatePropertyOption(SelectedProperties[i], typeInEdit);
                }
            }
        }

        private void SaveValues(BotType editingType)
        {
            BotDifficulty editingDiff = SelectedDifficulties[SelectedDifficulties.Count];
            foreach (var Property in SelectedProperties)
            {
                try
                {
                    object value = PresetManager.GetPresetValue(editingType, Property, editingDiff);
                    PresetManager.SetPresetValue(value, SelectedWildSpawnTypes, Property, SelectedDifficulties);
                }
                catch (Exception ex)
                {
                    Logger.LogError(Property.Name, GetType(), true);
                    Logger.LogError(ex.Message, GetType(), true);
                }
            }

            Reset();
        }

        private void Reset()
        {
            SelectedDifficulties.Clear();
            SelectedSections.Clear();
            SelectedWildSpawnTypes.Clear();
            SelectedProperties.Clear();
        }

        private void CreatePropertyOption(PropertyInfo property, BotType type)
        {
            BotDifficulty diff = SelectedDifficulties[SelectedDifficulties.Count];
            Type propertyType = property.PropertyType;
            if (propertyType == typeof(SAINProperty<float>))
            {
                var floatProperty = (SAINProperty<float>)property.GetValue(type.Preset);
                BuilderClass.HorizSlider(floatProperty, diff);
            }
            else if (propertyType == typeof(SAINProperty<bool>))
            {
                var boolProperty = (SAINProperty<bool>)property.GetValue(type.Preset);
                BuilderClass.CreateButtonOption(boolProperty, diff);
            }
        }

        private void CheckAddAll<T>(bool addAll, bool oldValue, List<T> list, T item)
        {
            if (addAll)
            {
                if (!list.Contains(item))
                {
                    list.Add(item);
                }
            }
        }

        private int SelectionGridOption<T>(int spacing, string optionName, List<T> list, T item, float optionPerLine = 3f, float optionHeight = 25f)
        {
            spacing = CheckSpacing(spacing, Mathf.RoundToInt(optionPerLine));

            bool selected = list.Contains(item);
            if (Toggle(selected, optionName, Height(optionHeight), Width(RectLayout.MainWindow.width / optionPerLine - 20f)))
            {
                if (!list.Contains(item))
                {
                    list.Add(item);
                }
            }
            else
            {
                if (list.Contains(item))
                {
                    list.Remove(item);
                }
            }
            if (CheckDragLayout())
            {
                if (!list.Contains(item))
                {
                    list.Add(item);
                }
            }
            return spacing;
        }

        private int CheckSpacing(int spacing, int check)
        {
            if (spacing == check)
            {
                spacing = 0;
                EndHorizontal();
                BeginHorizontal();
            }
            spacing++;
            return spacing;
        }

        private bool LabelAndAll(string labelName, bool value, float height = 35, float width = 200f)
        {
            BeginHorizontal();

            Box(labelName, true);
            value = Toggle(value, "All", true);

            FlexibleSpace();

            EndHorizontal();

            return value;
        }
    }
}