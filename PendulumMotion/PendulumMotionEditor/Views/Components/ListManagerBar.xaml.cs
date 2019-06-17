﻿using System;
using System.Collections.Generic;
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
using GKit;
using GKit.WPF;

namespace PendulumMotionEditor.Views.Components {
	/// <summary>
	/// ListManagerBar.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class ListManagerBar : UserControl {

		public event Action OnClick_CreateItemButton;
		public event Action OnClick_CreateFolderButton;
		public event Action OnClick_CopyButton;
		public event Action OnClick_RemoveButton;

		public ListManagerBar() {
			InitializeComponent();

			if (this.IsDesignMode())
				return;

			RegisterEvents();
		}
		private void RegisterEvents() {
			CreateItemButton.Click += (object sender, RoutedEventArgs e) => { OnClick_CreateItemButton?.Invoke(); };
			CreateFolderButton.Click += (object sender, RoutedEventArgs e) => { OnClick_CreateFolderButton?.Invoke(); };
			CopyButton.Click += (object sender, RoutedEventArgs e) => { OnClick_CopyButton?.Invoke(); };
			RemoveButton.Click += (object sender, RoutedEventArgs e) => { OnClick_RemoveButton?.Invoke(); };
		}
	}
}
