﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PendulumMotion;
using PendulumMotion.Component;
using PendulumMotion.Items;
using PendulumMotion.System;
using PendulumMotionEditor.Views.Components;
using PendulumMotionEditor.Views.Items;
using PendulumMotionEditor.Views.Windows;
using GKit;
using GKit.WPF;

namespace PendulumMotionEditor.Views.Contexts {
	/// <summary>
	/// MotionEditorContext.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class MotionEditorContext : UserControl {
		public MotionEditorContext Instance {
			get; private set;
		}

		public static Root Root {
			get; private set;
		}
		private static GLoopEngine LoopEngine => Root.loopEngine;
		private static CursorStorage CursorStorage => Root.cursorStorage;

		private const float SeparatorWidthHalf = 2f;

		public bool IsSaveMarked => editingFile == null || editingFile.CheckSave();


		//Preview
		private int PreviewFps => Mathf.Clamp(PreviewFpsEditText.textBox.Text.Parse2Int(60), 1, 1000);
		private float PreviewSeconds => Mathf.Clamp(PreviewSecondsEditText.textBox.Text.Parse2Float(1f), 0.02f, 1000f);
		private float ActualPreviewTime => Mathf.Clamp01(previewTime);
		private float previewTime;

		private GLoopEngine previewLoopEngine;
		private Stopwatch previewWatch;
		private float UpdateFPSTimer;
		private Ellipse[] previewContinuum;

		public bool OnEditing => editingFile != null;
		public EditableMotionFile editingFile;

		public MotionEditorContext() {
			InitializeComponent();
			if (this.IsDesignMode())
				return;

			Init();
			RegisterEvents();
		}
		private void Init() {
			Instance = this;

			Root = new Root(this);

			previewLoopEngine = new GLoopEngine(registInput: false);
			previewLoopEngine.MaxOverlapFrame = 3;
			previewWatch = new Stopwatch();

			CreatePreviewContinuum();
			MoveOrderPointer.Visibility = Visibility.Collapsed;
			MoveOrderCursor.Visibility = Visibility.Collapsed;

			PreviewFpsEditText.textBox.SetOnlyIntInput();
			PreviewSecondsEditText.textBox.SetOnlyFloatInput();
			PreviewFpsEditText.textBox.Text = 60.ToString();
			PreviewSecondsEditText.textBox.Text = 1.ToString();

			previewLoopEngine.StartLoop();
		}
		private void RegisterEvents() {
			const int TimeTextBoxMaxLength = 5;
			PreviewPositionCanvas.SizeChanged += OnSizeChanged_PreviewPositionCanvas;
			EditPanel.SizeChanged += OnSizeChanged_EditPanel;
			PreviewFpsEditText.LostFocus += OnLostFocus_PreviewFpsEditText;
			PreviewFpsEditText.KeyDown += OnKeyDown_PreviewFpsEditText;
			PreviewFpsEditText.textBox.MaxLength = TimeTextBoxMaxLength;
			PreviewSecondsEditText.LostFocus += OnLostFocus_PreviewSecondsEditText;
			PreviewSecondsEditText.KeyDown += OnKeyDown_PreviewSecondsEditText;
			PreviewSecondsEditText.textBox.MaxLength = TimeTextBoxMaxLength;

			//Motionlist button
			MLManagerBar.OnClick_CreateItemButton += OnClick_ML_CreateItemButton;
			MLManagerBar.OnClick_CreateFolderButton += OnClick_ML_CreateFolderButton;
			MLManagerBar.OnClick_CopyButton += OnClick_ML_CopyButton;
			MLManagerBar.OnClick_RemoveButton += OnClick_ML_RemoveButton;

			previewLoopEngine.AddLoopAction(OnPreviewTick);
			LoopEngine.AddGRoutine(UpdateItemPreviewRoutine());
		}

		private void OnPreviewTick() {

			const float OverTimeSec = 0.8f;

			float previewSec = PreviewSeconds;
			float previewFps = PreviewFps;
			float frameDelta = 1f / previewSec / previewFps;
			float maxOverTime = OverTimeSec / previewSec;

			SimulateTime(ref previewTime, frameDelta, maxOverTime);
			float motionTime = GetMotionTime(ActualPreviewTime);

			EditPanel.UpdatePlaybackRadar(previewTime, maxOverTime);
			UpdatePreviewPositionShape(motionTime);
			UpdatePreviewScaleShape(motionTime);
			UpdateInfoTexts(previewSec, previewFps);
		}
		private void OnSizeChanged_PreviewPositionCanvas(object sender, SizeChangedEventArgs e) {
			float motionTime = GetMotionTime(ActualPreviewTime);

			UpdatePreviewContinuum();
			UpdatePreviewPositionShape(motionTime);
		}
		private void OnSizeChanged_EditPanel(object sender, SizeChangedEventArgs e) {
			EditPanel.UpdateUI();
		}
		private void OnLostFocus_PreviewFpsEditText(object sender, RoutedEventArgs e) {
			UpdatePreviewFpsEditText();
		}
		private void OnLostFocus_PreviewSecondsEditText(object sender, RoutedEventArgs e) {
			UpdatePreviewSecondsEditText();
		}
		private void OnKeyDown_PreviewFpsEditText(object sender, KeyEventArgs e) {
			if (e.Key == Key.Return) {
				UpdatePreviewFpsEditText();
			}
		}
		private void OnKeyDown_PreviewSecondsEditText(object sender, KeyEventArgs e) {
			if (e.Key == Key.Return) {
				UpdatePreviewSecondsEditText();
			}
		}
		private void OnClick_ML_CreateItemButton() {
			PMMotion motion = editingFile.CreateMotion();
			editingFile.SelectItemSingle(motion);

			editingFile.MarkUnsaved();
		}
		private void OnClick_ML_CreateFolderButton() {
			PMFolder folder = editingFile.CreateFolder();
			editingFile.SelectItemSingle(folder);

			editingFile.MarkUnsaved();
		}
		private void OnClick_ML_RemoveButton() {
			editingFile.RemoveSelectedItems();

			editingFile.MarkUnsaved();
		}
		private void OnClick_ML_CopyButton() {
			editingFile.DuplicateSelectedMotion();

			editingFile.MarkUnsaved();
		}

		public void CreateNewFile() {
			ClearEditingData();
			editingFile = new EditableMotionFile();
		}
		public bool OpenFile() {
			EditableMotionFile openedFile = EditableMotionFile.Load();

			if (openedFile == null)
				return false;

			this.editingFile = openedFile;
			List<PMItemBase> rootItemList = editingFile.file.rootFolder.childList;
			if (rootItemList.Count > 0) {
				editingFile.SelectItemSingle(rootItemList[0]);
			}
			return true;
		}
		public void SaveFile() {
			editingFile.Save();
		}

		public void SetCopyButtonEnable(bool enable) {
			MLManagerBar.CopyButton.Opacity = enable ? 1f : 0.3f;
		}

		private void UpdatePreviewPositionShape(float motionTime) {
			double gridWidth = PreviewPositionCanvas.ActualWidth;
			double previewPos = gridWidth * motionTime - PreviewPositionShape.ActualWidth * 0.5f - SeparatorWidthHalf;
			Canvas.SetLeft(PreviewPositionShape, previewPos);
			Canvas.SetTop(PreviewPositionShape, (PreviewPositionCanvas.ActualHeight - PreviewPositionShape.Height) * 0.5f);
		}
		private void UpdatePreviewScaleShape(float motionTime) {
			PreviewScaleShape.RenderTransform = new ScaleTransform(motionTime, motionTime);
		}
		private void UpdateInfoTexts(float previewSec, float previewFps) {
			const float UpdateFpsTick = 0.5f;

			previewWatch.Stop();
			ActualFrameTextView.Text = $"({((int)(previewSec * previewFps))} Frame)";
			float deltaMillisec = previewWatch.GetElapsedMilliseconds();
			float deltaSec = deltaMillisec * 0.001f;
			if (UpdateFPSTimer < 0f) {
				if (deltaMillisec > 0.01f) {
					UpdateFPSTimer = UpdateFpsTick;
					ActualFPSTextView.Text = $"{(1f / deltaSec).ToString("0.0")} FPS";
				}
			} else {
				UpdateFPSTimer -= deltaSec;
			}
			previewWatch.Restart();
		}

		private void CreatePreviewContinuum() {
			const int ContinuumResolution = 20;
			const float ContinuumElementAlpha = 0.15f;

			previewContinuum = new Ellipse[ContinuumResolution];
			for (int i = 0; i < previewContinuum.Length; ++i) {
				Ellipse continuumElement = previewContinuum[i] = new Ellipse();

				continuumElement.SetParent(PreviewPositionCanvas);
				continuumElement.Width = PreviewPositionShape.Width;
				continuumElement.Height = PreviewPositionShape.Height;
				continuumElement.Fill = PreviewPositionShape.Fill;
				continuumElement.Stroke = PreviewPositionShape.Stroke;
				continuumElement.StrokeThickness = PreviewPositionShape.StrokeThickness;
				continuumElement.HorizontalAlignment = PreviewPositionShape.HorizontalAlignment;
				continuumElement.Opacity = ContinuumElementAlpha;
				Grid.SetColumn(continuumElement, Grid.GetColumn(PreviewPositionShape));
				Grid.SetColumnSpan(continuumElement, Grid.GetColumnSpan(PreviewPositionShape));
			}
			UpdatePreviewContinuum();
		}
		public void UpdatePreviewContinuum() {
			if (previewContinuum == null || !EditPanel.OnEditing)
				return;

			double gridWidth = PreviewPositionCanvas.ActualWidth;

			for (int i = 0; i < previewContinuum.Length; ++i) {
				float linearTime = (float)i / (previewContinuum.Length - 1);
				float motionTime = EditPanel.editingMotion.GetMotionValue(linearTime);

				Ellipse continuumElement = previewContinuum[i];

				Canvas.SetLeft(continuumElement, motionTime * gridWidth - PreviewPositionShape.ActualWidth * 0.5f - SeparatorWidthHalf);
				Canvas.SetTop(continuumElement, (PreviewPositionCanvas.ActualHeight - continuumElement.Height) * 0.5f);
			}
		}
		public void SetPreviewContinuumVisible(bool visible) {
			if (previewContinuum == null)
				return;

			for (int i = 0; i < previewContinuum.Length; ++i) {
				previewContinuum[i].Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
			}
		}

		public void ClearEditingData() {
			EditPanel.DetachMotion();
			MLItemContext.Children.Clear();
		}
		public void ResetPreviewTime() {
			previewTime = 0f;
		}
		public void ApplyPreviewFPS() {
			previewLoopEngine.FPS = PreviewFps;
		}

		private IEnumerator UpdateItemPreviewRoutine() {
			//UpdateItemPreview
			int iterCounter = 0;
			for (; ; ) {
				if (OnEditing) {
					yield return UpdateItemPreview(editingFile.file.rootFolder);
				}
				yield return new GWait(GTimeUnit.Frame, 1);
			}

			IEnumerator UpdateItemPreview(PMFolder folder) {
				for (int childI = 0; childI < folder.childList.Count; ++childI) {
					PMItemBase child = folder.childList[childI];
					switch (child.type) {
						case PMItemType.Motion:
							PMMotion motion = child.Cast<PMMotion>();
							motion.view.Cast<PMItemView>().UpdatePreviewGraph(motion);

							if (++iterCounter >= 2) {
								iterCounter = 0;
								yield return new GWait(GTimeUnit.Frame, 1);
							}
							break;
						case PMItemType.Folder:
							yield return UpdateItemPreview(child.Cast<PMFolder>());
							break;
					}
				}
			}
		}
		private void UpdatePreviewFpsEditText() {
			PreviewFpsEditText.textBox.Text = PreviewFps.ToString();
		}
		private void UpdatePreviewSecondsEditText() {
			PreviewSecondsEditText.textBox.Text = PreviewSeconds.ToString();
		}

		private void SimulateTime(ref float previewTime, float frameDelta, float maxOverTime) {
			previewTime += frameDelta;
			if (previewTime > 1f + maxOverTime || previewTime < -maxOverTime) {
				previewTime = -maxOverTime;
			}
		}
		private float GetMotionTime(float linearTime) {
			return EditPanel.OnEditing ? EditPanel.editingMotion.GetMotionValue(linearTime) : 0f;
		}
	}
}
