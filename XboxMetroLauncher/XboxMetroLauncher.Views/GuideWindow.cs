using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using XboxMetroLauncher.Input;
using XboxMetroLauncher.ViewModels;

namespace XboxMetroLauncher.Views;

public partial class GuideWindow : Window, IGuideWindow
{
	private readonly record struct OverlayFocusCandidate(Control Control, Rect Bounds);

	private readonly GuideViewModel _viewModel;

	private int _lastAnimatedMenuIndex = -1;

	private int _lastAnimatedMediaControlIndex = -1;

	private int _lastAnimatedMediaSubmenuIndex = -1;

	private int _lastAnimatedFriendListIndex = -1;

	private int _lastAnimatedPartyRowIndex = -1;

	private int _lastAnimatedSearchKeyIndex = -1;

	private int _lastAnimatedFriendProfileActionIndex = -1;

	private int _lastAnimatedAchievementIndex = -1;

	private int _pendingCommunitySwipeDirection;

	private readonly List<FrameworkElement> _guideOpeningSideElements = new List<FrameworkElement>();

	private readonly List<FrameworkElement> _guideOpeningUserLabelElements = new List<FrameworkElement>();

	private readonly List<(Border Border, Thickness BorderThickness)> _guideOpeningUserBorders = new List<(Border Border, Thickness BorderThickness)>();

	private bool _isOpening;

	private bool _isClosing;

	private const double GuideBladeLeft = 210.0;

	private const double GuideBladeTop = 50.0;

	private const double GuideBladeWidth = 860.0;

	private const double GuideBladeHeight = 606.0;

	private const double GuideOpenStartOffsetY = 0.0;

	private const double GuideOpenSettleOffsetY = 0.0;

	private const double GuideCloseEndOffsetY = -10.0;

	public bool IsGuideOpen
	{
		get
		{
			if (base.IsVisible && !_isOpening)
			{
				return !_isClosing;
			}
			return false;
		}
	}

	public bool IsTransitioning
	{
		get
		{
			if (!_isOpening)
			{
				return _isClosing;
			}
			return true;
		}
	}

	public event EventHandler? HiddenCompleted;

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool BringWindowToTop(nint hWnd);

	[DllImport("user32.dll")]
	private static extern nint SetFocus(nint hWnd);

	[DllImport("user32.dll")]
	private static extern nint SetActiveWindow(nint hWnd);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	public GuideWindow(GuideViewModel viewModel)
	{
		InitializeComponent();
		ConfigureGuideHeaderLayout();
		ApplyGuideBladeScale();
		_viewModel = viewModel;
		base.DataContext = viewModel;
		_viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
	}

	public bool Open(bool resetToHome = true)
	{
		if (_isOpening || _isClosing)
		{
			return false;
		}
		if (base.IsVisible)
		{
			if (resetToHome)
			{
				_viewModel.ResetToXboxHome();
			}
			ForceForegroundAndCaptureInput();
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(FocusGuideMenu), (DispatcherPriority)5, Array.Empty<object>());
			return false;
		}
		_isOpening = true;
		_isClosing = false;
		_viewModel.Start(resetToHome);
		ApplyGuideBladeScale();
		base.WindowState = WindowState.Maximized;
		base.Opacity = 0.0;
		GuideContentOffset.Y = GuideOpenStartOffsetY;
		MainGuidePanel.Opacity = 0.0;
		Show();
		BeginOpenAnimation();
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			try
			{
				ForceForegroundAndCaptureInput();
				FocusGuideMenu();
			}
			catch (Exception exception)
			{
				App.LogException(exception, "GuideWindow.Open.FocusGuideMenu");
			}
		}, (DispatcherPriority)5, Array.Empty<object>());
		return true;
	}

	private void ApplyGuideBladeScale()
	{
		SetGuideBladeBounds(AchievementsOverlay);
		SetGuideBladeBounds(FriendsListOverlay);
		SetGuideBladeBounds(PartyOverlay);
		SetGuideBladeBounds(FriendProfileOverlay);
		if (AchievementsOverlay.RowDefinitions.Count >= 2)
		{
			AchievementsOverlay.RowDefinitions[1].Height = new GridLength(526.0);
		}
		if (FriendsListOverlay.RowDefinitions.Count >= 2)
		{
			FriendsListOverlay.RowDefinitions[1].Height = new GridLength(526.0);
		}
		if (PartyOverlay.RowDefinitions.Count >= 2)
		{
			PartyOverlay.RowDefinitions[1].Height = new GridLength(526.0);
		}
		if (FriendProfileOverlay.RowDefinitions.Count >= 2)
		{
			FriendProfileOverlay.RowDefinitions[0].Height = new GridLength(40.0);
			FriendProfileOverlay.RowDefinitions[1].Height = new GridLength(526.0);
		}
	}

	private static void SetGuideBladeBounds(FrameworkElement blade)
	{
		Canvas.SetLeft(blade, 210.0);
		Canvas.SetTop(blade, 50.0);
		blade.Width = 860.0;
		blade.Height = 606.0;
	}

	public bool CloseGuide(bool playSound = false)
	{
		if (_isClosing || _isOpening || !base.IsVisible)
		{
			return false;
		}
		if (playSound)
		{
			_viewModel.PlaySound("guide-close");
		}
		_isClosing = true;
		_viewModel.Stop();
		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(165.0))
		{
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseIn
			}
		};
		doubleAnimation.Completed += delegate
		{
			ReleaseInputCapture();
			Hide();
			base.Opacity = 1.0;
			GuideContentOffset.Y = GuideOpenStartOffsetY;
			GuideBladeOffset.Y = 0.0;
			GuideBladePanel.Opacity = 1.0;
			ResetGuideOpeningReveal();
			_isClosing = false;
			this.HiddenCompleted?.Invoke(this, EventArgs.Empty);
		};
		BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		GuideBladeOffset.BeginAnimation(TranslateTransform.YProperty, null);
		GuideBladePanel.BeginAnimation(UIElement.OpacityProperty, null);
		GuideContentOffset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(GuideCloseEndOffsetY, TimeSpan.FromMilliseconds(190.0))
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseIn
			}
		});
		GuideBladePanel.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.72, TimeSpan.FromMilliseconds(115.0))
		{
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseIn
			}
		});
		return true;
	}

	public bool HandleInput(DashboardInputAction action)
	{
		if (_viewModel.IsGuideMusicPickerScreen)
		{
			switch (action)
			{
			case DashboardInputAction.MoveLeft:
			case DashboardInputAction.MoveRight:
			case DashboardInputAction.MoveUp:
			case DashboardInputAction.MoveDown:
				TryMoveGuideMusicFocus(action);
				return true;
			case DashboardInputAction.Activate:
				ActivateFocusedGuideMusicControl();
				return true;
			case DashboardInputAction.Back:
				if (_viewModel.HandleBack())
				{
					FocusGuideMenu();
					return true;
				}
				CloseGuide(playSound: true);
				return true;
			case DashboardInputAction.Details:
				if (_viewModel.Dashboard.OpenMusicVisualizerFullscreenCommand.CanExecute(null))
				{
					_viewModel.Dashboard.OpenMusicVisualizerFullscreenCommand.Execute(null);
				}
				return true;
			}
		}
		switch (action)
		{
		case DashboardInputAction.MoveUp:
			_viewModel.Move(-1);
			FocusGuideMenu();
			return true;
		case DashboardInputAction.MoveDown:
			_viewModel.Move(1);
			FocusGuideMenu();
			return true;
		case DashboardInputAction.MoveLeft:
			if (!_viewModel.TryHandleHorizontal(-1))
			{
				_viewModel.MoveTab(-1);
			}
			FocusGuideMenu();
			return true;
		case DashboardInputAction.MoveRight:
			if (!_viewModel.TryHandleHorizontal(1))
			{
				_viewModel.MoveTab(1);
			}
			FocusGuideMenu();
			return true;
		case DashboardInputAction.PreviousTab:
			if (_viewModel.IsFriendSearchScreen)
			{
				_viewModel.ActivateSearchCursorLeftKey();
				FocusGuideMenu();
				return true;
			}
			if (_viewModel.IsFriendsListScreen || _viewModel.IsPartyScreen)
			{
				FocusGuideMenu();
				return true;
			}
			RememberCommunitySwipeDirection(-1);
			if (_viewModel.SwitchCommunityTab(-1))
			{
				FocusGuideMenu();
				return true;
			}
			_viewModel.MoveTab(-1);
			FocusGuideMenu();
			return true;
		case DashboardInputAction.NextTab:
			if (_viewModel.IsFriendSearchScreen)
			{
				_viewModel.ActivateSearchCursorRightKey();
				FocusGuideMenu();
				return true;
			}
			if (_viewModel.IsFriendsListScreen || _viewModel.IsPartyScreen)
			{
				FocusGuideMenu();
				return true;
			}
			RememberCommunitySwipeDirection(1);
			if (_viewModel.SwitchCommunityTab(1))
			{
				FocusGuideMenu();
				return true;
			}
			_viewModel.MoveTab(1);
			FocusGuideMenu();
			return true;
		case DashboardInputAction.LeftTrigger:
			_viewModel.ActivateSearchSymbolsKey();
			FocusGuideMenu();
			return true;
		case DashboardInputAction.RightTrigger:
			_viewModel.ActivateSearchAccentsKey();
			FocusGuideMenu();
			return true;
		case DashboardInputAction.Activate:
			_viewModel.ActivateSelected();
			FocusGuideMenu();
			return true;
		case DashboardInputAction.Back:
			if (_viewModel.HandleBack())
			{
				FocusGuideMenu();
				return true;
			}
			CloseGuide(playSound: true);
			return true;
		case DashboardInputAction.Details:
			_viewModel.HandleFooterX();
			return true;
		case DashboardInputAction.Search:
			_viewModel.HandleFooterY();
			return true;
		case DashboardInputAction.Options:
			if (_viewModel.IsFriendSearchScreen)
			{
				_viewModel.ActivateSearchDoneKey();
				FocusGuideMenu();
			}
			return true;
		case DashboardInputAction.LeftThumbClick:
			if (_viewModel.IsFriendSearchScreen)
			{
				_viewModel.ActivateSearchCapsKey();
				FocusGuideMenu();
			}
			return true;
		case DashboardInputAction.Guide:
			CloseGuide(playSound: true);
			return true;
		default:
			return true;
		}
	}

	private void BackHint_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
		if (_viewModel.HandleBack())
		{
			FocusGuideMenu();
			return;
		}
		CloseGuide(playSound: true);
	}

	private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Invalid comparison between Unknown and I4
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Invalid comparison between Unknown and I4
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Invalid comparison between Unknown and I4
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Invalid comparison between Unknown and I4
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Invalid comparison between Unknown and I4
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Invalid comparison between Unknown and I4
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Invalid comparison between Unknown and I4
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Invalid comparison between Unknown and I4
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Invalid comparison between Unknown and I4
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Invalid comparison between Unknown and I4
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Invalid comparison between Unknown and I4
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Invalid comparison between Unknown and I4
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Invalid comparison between Unknown and I4
		//IL_0282: Unknown result type (might be due to invalid IL or missing references)
		//IL_0285: Invalid comparison between Unknown and I4
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Invalid comparison between Unknown and I4
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c0: Invalid comparison between Unknown and I4
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Invalid comparison between Unknown and I4
		//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c5: Invalid comparison between Unknown and I4
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Invalid comparison between Unknown and I4
		//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fe: Invalid comparison between Unknown and I4
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Invalid comparison between Unknown and I4
		//IL_0319: Unknown result type (might be due to invalid IL or missing references)
		//IL_0320: Invalid comparison between Unknown and I4
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Invalid comparison between Unknown and I4
		//IL_033b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0340: Unknown result type (might be due to invalid IL or missing references)
		//IL_0341: Unknown result type (might be due to invalid IL or missing references)
		//IL_0343: Invalid comparison between Unknown and I4
		//IL_0345: Unknown result type (might be due to invalid IL or missing references)
		//IL_0348: Invalid comparison between Unknown and I4
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Invalid comparison between Unknown and I4
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Invalid comparison between Unknown and I4
		//IL_0368: Unknown result type (might be due to invalid IL or missing references)
		//IL_036d: Unknown result type (might be due to invalid IL or missing references)
		//IL_036e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0370: Invalid comparison between Unknown and I4
		//IL_0372: Unknown result type (might be due to invalid IL or missing references)
		//IL_0375: Invalid comparison between Unknown and I4
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Invalid comparison between Unknown and I4
		//IL_03a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03af: Invalid comparison between Unknown and I4
		//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cd: Invalid comparison between Unknown and I4
		if (_viewModel.IsFriendSearchScreen)
		{
			if ((int)e.Key == 2)
			{
				_viewModel.BackspaceFriendSearchFromKeyboard();
				e.Handled = true;
			}
			else if ((int)e.Key == 6)
			{
				_viewModel.ConfirmFriendSearch();
				e.Handled = true;
			}
			else if ((int)e.Key == 13 && _viewModel.HandleBack())
			{
				e.Handled = true;
			}
		}
		if (e.Handled)
		{
			FocusGuideMenu();
		}
		else
		{
			if (_viewModel.IsFriendSearchScreen && IsPhysicalTypingKey(e.Key))
			{
				return;
			}
			Key key;
			if (_viewModel.IsGuideMusicPickerScreen)
			{
				key = e.Key;
				if (((int)key == 24 || (int)key == 66) ? true : false)
				{
					TryMoveGuideMusicFocus(DashboardInputAction.MoveUp);
					e.Handled = true;
					return;
				}
				key = e.Key;
				if (((int)key == 26 || (int)key == 62) ? true : false)
				{
					TryMoveGuideMusicFocus(DashboardInputAction.MoveDown);
					e.Handled = true;
					return;
				}
				key = e.Key;
				if (((int)key == 23 || (int)key == 44) ? true : false)
				{
					TryMoveGuideMusicFocus(DashboardInputAction.MoveLeft);
					e.Handled = true;
					return;
				}
				key = e.Key;
				if (((int)key == 25 || (int)key == 47) ? true : false)
				{
					TryMoveGuideMusicFocus(DashboardInputAction.MoveRight);
					e.Handled = true;
					return;
				}
				key = e.Key;
				if (((int)key == 6 || (int)key == 18) ? true : false)
				{
					ActivateFocusedGuideMusicControl();
					e.Handled = true;
					return;
				}
				key = e.Key;
				if (((int)key == 2 || (int)key == 13) ? true : false)
				{
					if (_viewModel.HandleBack())
					{
						FocusGuideMenu();
						e.Handled = true;
					}
					else
					{
						CloseGuide(playSound: true);
						e.Handled = true;
					}
				}
				else if ((int)e.Key == 67)
				{
					if (_viewModel.Dashboard.OpenMusicVisualizerFullscreenCommand.CanExecute(null))
					{
						_viewModel.Dashboard.OpenMusicVisualizerFullscreenCommand.Execute(null);
					}
					e.Handled = true;
				}
				return;
			}
			key = e.Key;
			if (((int)key == 24 || (int)key == 66) ? true : false)
			{
				_viewModel.Move(-1);
				e.Handled = true;
			}
			else
			{
				key = e.Key;
				if (((int)key == 26 || (int)key == 62) ? true : false)
				{
					_viewModel.Move(1);
					e.Handled = true;
				}
				else
				{
					key = e.Key;
					if (((int)key == 23 || (int)key == 44) ? true : false)
					{
						if (!_viewModel.TryHandleKeyboardHorizontal(-1))
						{
							_viewModel.MoveTab(-1);
						}
						e.Handled = true;
					}
					else
					{
						key = e.Key;
						if (((int)key == 25 || (int)key == 47) ? true : false)
						{
							if (!_viewModel.TryHandleKeyboardHorizontal(1))
							{
								_viewModel.MoveTab(1);
							}
							e.Handled = true;
						}
						else if ((int)e.Key == 60)
						{
							_viewModel.MoveTab(-1);
							e.Handled = true;
						}
						else if ((int)e.Key == 48)
						{
							_viewModel.MoveTab(1);
							e.Handled = true;
						}
						else
						{
							key = e.Key;
							if (((int)key == 6 || (int)key == 18) ? true : false)
							{
								_viewModel.ActivateSelected();
								e.Handled = true;
							}
							else
							{
								key = e.Key;
								if (((int)key == 2 || (int)key == 13) ? true : false)
								{
									if (_viewModel.HandleBack())
									{
										e.Handled = true;
									}
									else
									{
										CloseGuide(playSound: true);
										e.Handled = true;
									}
								}
								else if ((int)e.Key == 67)
								{
									_viewModel.HandleFooterX();
									e.Handled = true;
								}
								else if ((int)e.Key == 68)
								{
									_viewModel.HandleFooterY();
									e.Handled = true;
								}
							}
						}
					}
				}
			}
			if (e.Handled)
			{
				FocusGuideMenu();
			}
		}
	}

	private void Window_OnTextInput(object sender, TextCompositionEventArgs e)
	{
		if (!_viewModel.IsFriendSearchScreen || string.IsNullOrEmpty(e.Text))
		{
			return;
		}
		string text = e.Text;
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			if (!char.IsControl(c))
			{
				_viewModel.AppendFriendSearchCharacter(c.ToString());
			}
		}
		e.Handled = true;
		FocusGuideMenu();
	}

	private void FocusGuideMenu()
	{
		if (_viewModel.IsGuideMusicPickerScreen)
		{
			FocusGuideMusicOverlay();
			return;
		}
		if (_viewModel.IsFriendsListScreen)
		{
			FocusFriendsList();
			return;
		}
		if (_viewModel.IsPartyScreen)
		{
			FocusPartyRows();
			return;
		}
		if (_viewModel.IsFriendSearchScreen)
		{
			FocusSearchKeys();
			return;
		}
		if (_viewModel.IsFriendProfileScreen)
		{
			FocusFriendProfileActions();
			return;
		}
		if (_viewModel.IsAchievementsScreen)
		{
			FocusAchievements();
			return;
		}
		if (_viewModel.IsMediaSubmenuOpen)
		{
			FocusMediaSubmenu();
			return;
		}
		if (_viewModel.IsMediaTab && _viewModel.IsMediaSongRowFocused)
		{
			MediaSongRowButton.Focus();
			return;
		}
		if (_viewModel.IsMediaTab && _viewModel.IsMediaTransportFocused)
		{
			FocusMediaTransport();
			return;
		}
		if (_viewModel.IsGuideMenuSelectionActive)
		{
			GuideMenu.SelectedIndex = _viewModel.SelectedIndex;
		}
		GuideMenu.UpdateLayout();
		GuideMenu.Focus();
		if (_viewModel.Items.Count == 0)
		{
			return;
		}
		ListBoxItem obj = GuideMenu.ItemContainerGenerator.ContainerFromIndex(_viewModel.SelectedIndex) as ListBoxItem;
		if (obj == null)
		{
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(FocusGuideMenu), (DispatcherPriority)5, Array.Empty<object>());
			return;
		}
		obj.Focus();
		AnimateSelectedListItem(obj, _viewModel.SelectedIndex, ref _lastAnimatedMenuIndex);
	}

	private void FocusFriendsList()
	{
		_viewModel.EnsureFriendListSelection();
		ListBox listBox = FriendsOverlayListBox.IsVisible ? FriendsOverlayListBox : FriendsListBox;
		int selectedIndex = _viewModel.SelectedFriendListIndex;
		if (selectedIndex < 0 || selectedIndex >= listBox.Items.Count)
		{
			listBox.Focus();
			return;
		}
		object selectedItem = listBox.Items[selectedIndex];
		listBox.SelectedIndex = selectedIndex;
		listBox.ScrollIntoView(selectedItem);
		listBox.UpdateLayout();
		listBox.Focus();
		ListBoxItem obj = listBox.ItemContainerGenerator.ContainerFromIndex(selectedIndex) as ListBoxItem;
		if (obj == null)
		{
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(FocusFriendsList), (DispatcherPriority)5, Array.Empty<object>());
			return;
		}
		obj.Focus();
		_lastAnimatedFriendListIndex = selectedIndex;
	}

	private void FocusPartyRows()
	{
		PartyOverlayListBox.UpdateLayout();
		PartyOverlayListBox.Focus();
		ListBoxItem obj = PartyOverlayListBox.ItemContainerGenerator.ContainerFromIndex(_viewModel.SelectedPartyRowIndex) as ListBoxItem;
		obj?.Focus();
		AnimateSelectedListItem(obj, _viewModel.SelectedPartyRowIndex, ref _lastAnimatedPartyRowIndex);
	}

	private void FocusSearchKeys()
	{
		Button button = FindSearchKeyButton(_viewModel.SelectedSearchKeyIndex);
		if (button == null)
		{
			GuideMenu.Focus();
			return;
		}
		button.Focus();
		AnimateSelectedButton(button, _viewModel.SelectedSearchKeyIndex, ref _lastAnimatedSearchKeyIndex);
	}

	private void FocusFriendProfileActions()
	{
		FriendProfileOverlayActionsList.UpdateLayout();
		FriendProfileOverlayActionsList.Focus();
		ListBoxItem obj = FriendProfileOverlayActionsList.ItemContainerGenerator.ContainerFromIndex(_viewModel.SelectedFriendProfileActionIndex) as ListBoxItem;
		obj?.Focus();
		AnimateSelectedListItem(obj, _viewModel.SelectedFriendProfileActionIndex, ref _lastAnimatedFriendProfileActionIndex);
	}

	private void FocusAchievements()
	{
		if (_viewModel.IsAchievementGameList)
		{
			if (_viewModel.SelectedAchievementGameIndex >= 0 && _viewModel.SelectedAchievementGameIndex < _viewModel.AchievementGameItems.Count)
			{
				AchievementsGameListBox.ScrollIntoView(_viewModel.AchievementGameItems[_viewModel.SelectedAchievementGameIndex]);
			}
			AchievementsGameListBox.UpdateLayout();
			AchievementsGameListBox.Focus();
			ListBoxItem obj = AchievementsGameListBox.ItemContainerGenerator.ContainerFromIndex(_viewModel.SelectedAchievementGameIndex) as ListBoxItem;
			obj?.Focus();
			AnimateSelectedListItem(obj, _viewModel.SelectedAchievementGameIndex, ref _lastAnimatedAchievementIndex);
			return;
		}
		if (_viewModel.SelectedAchievementIndex >= 0 && _viewModel.SelectedAchievementIndex < _viewModel.AchievementItems.Count)
		{
			AchievementsOverlayListBox.ScrollIntoView(_viewModel.AchievementItems[_viewModel.SelectedAchievementIndex]);
		}
		AchievementsOverlayListBox.UpdateLayout();
		AchievementsOverlayListBox.Focus();
		ListBoxItem obj2 = AchievementsOverlayListBox.ItemContainerGenerator.ContainerFromIndex(_viewModel.SelectedAchievementIndex) as ListBoxItem;
		obj2?.BringIntoView();
		obj2?.Focus();
		AnimateSelectedListItem(obj2, _viewModel.SelectedAchievementIndex, ref _lastAnimatedAchievementIndex);
	}

	private void FocusGuideMusicOverlay()
	{
		TryFocus(FindFocusableControl((DependencyObject?)(object)GuideMusicOverlay));
	}

	private void FocusMediaSubmenu()
	{
		MediaSubmenuList.UpdateLayout();
		MediaSubmenuList.Focus();
		ListBoxItem obj = MediaSubmenuList.ItemContainerGenerator.ContainerFromIndex(_viewModel.SelectedMediaSubmenuIndex) as ListBoxItem;
		obj?.Focus();
		AnimateSelectedListItem(obj, _viewModel.SelectedMediaSubmenuIndex, ref _lastAnimatedMediaSubmenuIndex);
	}

	private void FocusMediaTransport()
	{
		MediaTransportItems.UpdateLayout();
		Button button = FindVisualChild<Button>(MediaTransportItems.ItemContainerGenerator.ContainerFromIndex(_viewModel.SelectedMediaControlIndex));
		if (button != null)
		{
			button.Focus();
			AnimateSelectedButton(button, _viewModel.SelectedMediaControlIndex, ref _lastAnimatedMediaControlIndex);
		}
		else
		{
			GuideMenu.Focus();
		}
	}

	private void BeginOpenAnimation()
	{
		BeginAnimation(UIElement.OpacityProperty, null);
		GuideContentScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		GuideContentScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		GuideContentOffset.BeginAnimation(TranslateTransform.YProperty, null);
		GuideBladePanel.BeginAnimation(UIElement.OpacityProperty, null);
		GuideBladeOffset.BeginAnimation(TranslateTransform.YProperty, null);
		MainGuidePanel.BeginAnimation(UIElement.OpacityProperty, null);
		PrepareGuideOpeningReveal();
		base.Opacity = 0.0;
		GuideContentScale.ScaleX = 0.84;
		GuideContentScale.ScaleY = 0.84;
		GuideContentOffset.Y = GuideOpenStartOffsetY;
		GuideBladeOffset.Y = 0.0;
		GuideBladePanel.Opacity = 1.0;
		CubicEase guideOpenEase = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(70.0))
		{
			EasingFunction = guideOpenEase
		};
		BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		GuideContentScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(210.0))
		{
			EasingFunction = guideOpenEase
		});
		GuideContentScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(210.0))
		{
			EasingFunction = guideOpenEase
		});
		GuideContentOffset.Y = 0.0;
		DoubleAnimation doubleAnimation2 = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(145.0))
		{
			BeginTime = TimeSpan.FromMilliseconds(285.0),
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		MainGuidePanel.BeginAnimation(UIElement.OpacityProperty, doubleAnimation2);
		BeginGuideOpeningRevealAnimation(_guideOpeningUserLabelElements, TimeSpan.FromMilliseconds(250.0), TimeSpan.FromMilliseconds(120.0), null);
		BeginGuideOpeningRevealAnimation(_guideOpeningSideElements, TimeSpan.FromMilliseconds(430.0), TimeSpan.FromMilliseconds(145.0), delegate
		{
			_isOpening = false;
			GuideContentScale.ScaleX = 1.0;
			GuideContentScale.ScaleY = 1.0;
			GuideBladeOffset.Y = 0.0;
			GuideBladePanel.Opacity = 1.0;
			ResetGuideOpeningReveal();
			_viewModel.PlaySound("guide-blade-open");
		});
	}

	private void ConfigureGuideHeaderLayout()
	{
		foreach (StackPanel item in FindVisualChildren<StackPanel>((DependencyObject)(object)GuideContent))
		{
			if (Math.Abs(Canvas.GetLeft(item) - 802.0) < 0.1 && Math.Abs(Canvas.GetTop(item) - 153.0) < 0.1)
			{
				item.Visibility = Visibility.Collapsed;
				break;
			}
		}
		foreach (TextBlock item2 in FindVisualChildren<TextBlock>((DependencyObject)(object)GuideContent))
		{
			if (Math.Abs(Canvas.GetLeft(item2) - 777.0) < 0.1 && Math.Abs(Canvas.GetTop(item2) - 179.0) < 0.1)
			{
				Canvas.SetLeft(item2, 790.0);
				Canvas.SetTop(item2, 166.0);
				item2.Width = 150.0;
				item2.TextAlignment = TextAlignment.Left;
				break;
			}
		}
	}

	private void PrepareGuideOpeningReveal()
	{
		ResetGuideOpeningReveal();
		_guideOpeningSideElements.Clear();
		_guideOpeningUserLabelElements.Clear();
		_guideOpeningUserBorders.Clear();
		foreach (FrameworkElement item in GuideBladePanel.Children.OfType<FrameworkElement>())
		{
			int column = Grid.GetColumn(item);
			if (column == 0 || column == 3 || column == 4)
			{
				item.BeginAnimation(UIElement.OpacityProperty, null);
				item.Opacity = 0.0;
				_guideOpeningSideElements.Add(item);
			}
			else if (column == 1)
			{
				if (item is Border border)
				{
					_guideOpeningUserBorders.Add((border, border.BorderThickness));
					border.BorderThickness = new Thickness(0.0);
				}
				foreach (FrameworkElement item2 in FindVisualChildren<TextBlock>((DependencyObject)(object)item))
				{
					item2.BeginAnimation(UIElement.OpacityProperty, null);
					item2.Opacity = 0.0;
					_guideOpeningUserLabelElements.Add(item2);
				}
			}
		}
		MainGuidePanel.BeginAnimation(UIElement.OpacityProperty, null);
		MainGuidePanel.Opacity = 0.0;
	}

	private void ResetGuideOpeningReveal()
	{
		MainGuidePanel.BeginAnimation(UIElement.OpacityProperty, null);
		MainGuidePanel.Opacity = 1.0;
		foreach (FrameworkElement item in _guideOpeningSideElements)
		{
			item.BeginAnimation(UIElement.OpacityProperty, null);
			item.Opacity = 1.0;
		}
		foreach (FrameworkElement item2 in _guideOpeningUserLabelElements)
		{
			item2.BeginAnimation(UIElement.OpacityProperty, null);
			item2.Opacity = 1.0;
		}
		foreach (var item3 in _guideOpeningUserBorders)
		{
			item3.Border.BorderThickness = item3.BorderThickness;
		}
	}

	private static void BeginGuideOpeningRevealAnimation(IEnumerable<FrameworkElement> elements, TimeSpan beginTime, TimeSpan duration, EventHandler? completed)
	{
		DoubleAnimation animation = new DoubleAnimation(1.0, duration)
		{
			BeginTime = beginTime,
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		bool flag = false;
		foreach (FrameworkElement element in elements)
		{
			DoubleAnimation doubleAnimation = animation.Clone();
			if (!flag && completed != null)
			{
				flag = true;
				doubleAnimation.Completed += completed;
			}
			element.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
		}
		if (!flag)
		{
			completed?.Invoke(null, EventArgs.Empty);
		}
	}

	private void ForceForegroundAndCaptureInput()
	{
		WindowInteropHelper windowInteropHelper = new WindowInteropHelper(this);
		nint num = windowInteropHelper.Handle;
		if (num == IntPtr.Zero)
		{
			num = windowInteropHelper.EnsureHandle();
		}
		nint foregroundWindow = GetForegroundWindow();
		uint currentThreadId = GetCurrentThreadId();
		uint processId;
		uint num2 = ((foregroundWindow != IntPtr.Zero) ? GetWindowThreadProcessId(foregroundWindow, out processId) : 0u);
		bool flag = false;
		try
		{
			if (num2 != 0 && num2 != currentThreadId)
			{
				flag = AttachThreadInput(currentThreadId, num2, fAttach: true);
			}
			Activate();
			BringWindowToTop(num);
			SetForegroundWindow(num);
			SetActiveWindow(num);
			SetFocus(num);
			Focus();
			Keyboard.Focus(GuideRoot);
			Mouse.Capture(GuideRoot, CaptureMode.SubTree);
		}
		finally
		{
			if (flag)
			{
				AttachThreadInput(currentThreadId, num2, fAttach: false);
			}
		}
	}

	private static void ReleaseInputCapture()
	{
		if (Mouse.Captured != null)
		{
			Mouse.Capture(null);
		}
	}

	private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "CurrentTabTitle")
		{
			BeginBladeTransition(_viewModel.TabTransitionDirection);
			_lastAnimatedMenuIndex = -1;
			_lastAnimatedMediaControlIndex = -1;
			_lastAnimatedMediaSubmenuIndex = -1;
			_lastAnimatedFriendListIndex = -1;
			_lastAnimatedPartyRowIndex = -1;
			_lastAnimatedSearchKeyIndex = -1;
			_lastAnimatedFriendProfileActionIndex = -1;
		}
		if (e.PropertyName == "IsMediaSubmenuOpen" && _viewModel.IsMediaSubmenuOpen)
		{
			BeginMediaSubmenuOpenAnimation();
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(FocusGuideMenu), Array.Empty<object>());
		}
		if (e.PropertyName == "IsFriendsListScreen" && _viewModel.IsFriendsListScreen)
		{
			int pendingCommunitySwipeDirection = _pendingCommunitySwipeDirection;
			bool isOpeningFromMainGuide = _viewModel.ConsumeCommunityOverlayOpenFromMainGuide();
			_lastAnimatedFriendListIndex = -1;
			BeginCommunityOverlayAnimation(FriendsListOverlay, FriendsListOverlayOffset, FriendsListOverlayScale, FriendsOverlayHeader, FriendsOverlayMenuContent, FriendsOverlayFooter, GetCommunityOverlayStartX(18.0), pendingCommunitySwipeDirection, isOpeningFromMainGuide);
			BeginCommunityTabStripAnimation(FriendsCommunityTabStrip, FriendsActiveCommunityTab, FriendsActiveCommunityTabOffset, pendingCommunitySwipeDirection);
			_pendingCommunitySwipeDirection = 0;
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(FocusGuideMenu), Array.Empty<object>());
		}
		if (e.PropertyName == "FriendsListItems" && _viewModel.IsFriendsListScreen)
		{
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(FocusFriendsList), (DispatcherPriority)5, Array.Empty<object>());
		}
		if (e.PropertyName == "IsPartyScreen" && _viewModel.IsPartyScreen)
		{
			int pendingCommunitySwipeDirection2 = _pendingCommunitySwipeDirection;
			bool isOpeningFromMainGuide2 = _viewModel.ConsumeCommunityOverlayOpenFromMainGuide();
			_lastAnimatedPartyRowIndex = -1;
			BeginCommunityOverlayAnimation(PartyOverlay, PartyOverlayOffset, PartyOverlayScale, PartyOverlayHeader, PartyOverlayMenuContent, PartyOverlayFooter, GetCommunityOverlayStartX(18.0), pendingCommunitySwipeDirection2, isOpeningFromMainGuide2);
			BeginCommunityTabStripAnimation(PartyCommunityTabStrip, PartyActiveCommunityTab, PartyActiveCommunityTabOffset, pendingCommunitySwipeDirection2);
			_pendingCommunitySwipeDirection = 0;
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(FocusGuideMenu), Array.Empty<object>());
		}
		if (e.PropertyName == "IsFriendSearchScreen" && _viewModel.IsFriendSearchScreen)
		{
			_lastAnimatedSearchKeyIndex = -1;
			BeginOverlayOpenAnimation(FriendSearchOverlay, FriendSearchOverlayOffset, 18.0);
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(FocusGuideMenu), Array.Empty<object>());
		}
		if (e.PropertyName == "IsFriendProfileScreen" && _viewModel.IsFriendProfileScreen)
		{
			_lastAnimatedFriendProfileActionIndex = -1;
			BeginOverlayOpenAnimation(FriendProfileOverlay, FriendProfileOverlayOffset, 16.0);
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(FocusGuideMenu), Array.Empty<object>());
		}
		if (e.PropertyName == "IsAchievementsScreen" && _viewModel.IsAchievementsScreen)
		{
			bool isOpeningFromMainGuide3 = _viewModel.ConsumeAchievementsOverlayOpenFromMainGuide();
			_lastAnimatedAchievementIndex = -1;
			BeginCommunityOverlayAnimation(AchievementsOverlay, AchievementsOverlayOffset, AchievementsOverlayScale, AchievementsOverlayHeader, AchievementsOverlayMenuContent, AchievementsOverlayFooter, 18.0, 0, isOpeningFromMainGuide3);
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(FocusGuideMenu), Array.Empty<object>());
		}
		if (e.PropertyName == "IsAchievementDetail" && _viewModel.IsAchievementDetail)
		{
			_lastAnimatedAchievementIndex = -1;
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(FocusAchievements), (DispatcherPriority)5, Array.Empty<object>());
		}
		if (e.PropertyName == "IsGuideMusicPickerScreen" && _viewModel.IsGuideMusicPickerScreen)
		{
			bool isOpeningFromMainGuide4 = _viewModel.ConsumeGuideMusicPickerOpenFromMainGuide();
			BeginCommunityOverlayAnimation(GuideMusicOverlay, GuideMusicOverlayOffset, GuideMusicOverlayScale, GuideMusicHeader, GuideMusicMenuContent, GuideMusicFooter, 18.0, 0, isOpeningFromMainGuide4);
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(FocusGuideMenu), Array.Empty<object>());
		}
	}

	private void BeginMediaSubmenuOpenAnimation()
	{
		MediaSubmenuBlade.Opacity = 0.0;
		MediaSubmenuOffset.X = 22.0;
		MediaSubmenuBlade.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(105.0))
		{
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
		MediaSubmenuOffset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(145.0))
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
	}

	private static void BeginOverlayOpenAnimation(FrameworkElement element, TranslateTransform offset, double fromX)
	{
		element.BeginAnimation(UIElement.OpacityProperty, null);
		offset.BeginAnimation(TranslateTransform.XProperty, null);
		element.Opacity = 0.0;
		offset.X = fromX;
		element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(110.0))
		{
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
		offset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(145.0))
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
	}

	private static void BeginCommunityOverlayAnimation(FrameworkElement element, TranslateTransform offset, ScaleTransform scale, FrameworkElement header, FrameworkElement menuContent, FrameworkElement footer, double fromX, int direction, bool isOpeningFromMainGuide)
	{
		if (isOpeningFromMainGuide)
		{
			BeginCommunityOverlayOpenAnimation(element, offset, scale, header, menuContent, footer);
			return;
		}
		ResetCommunityOverlayContent(header, menuContent, footer);
		if (direction == 0)
		{
			BeginOverlayOpenAnimation(element, offset, fromX);
			return;
		}
		element.BeginAnimation(UIElement.OpacityProperty, null);
		offset.BeginAnimation(TranslateTransform.XProperty, null);
		scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		element.Opacity = 1.0;
		offset.X = fromX;
		scale.ScaleX = 1.0;
		scale.ScaleY = 1.0;
		offset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(260.0))
		{
			EasingFunction = new QuarticEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
	}

	private static void BeginCommunityOverlayOpenAnimation(FrameworkElement element, TranslateTransform offset, ScaleTransform scale, FrameworkElement header, FrameworkElement menuContent, FrameworkElement footer)
	{
		element.BeginAnimation(UIElement.OpacityProperty, null);
		offset.BeginAnimation(TranslateTransform.XProperty, null);
		offset.BeginAnimation(TranslateTransform.YProperty, null);
		scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		PrepareCommunityOverlayContent(header, menuContent, footer);
		element.Opacity = 0.0;
		offset.X = 0.0;
		offset.Y = -30.0;
		scale.ScaleX = 0.93;
		scale.ScaleY = 0.86;
		CubicEase cubicEase = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(140.0))
		{
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
		offset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(260.0))
		{
			EasingFunction = cubicEase
		});
		scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(275.0))
		{
			EasingFunction = cubicEase
		});
		scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(275.0))
		{
			EasingFunction = cubicEase
		});
		BeginCommunityOverlayContentFade(header, TimeSpan.FromMilliseconds(105.0));
		BeginCommunityOverlayContentFade(menuContent, TimeSpan.FromMilliseconds(155.0));
		BeginCommunityOverlayContentFade(footer, TimeSpan.FromMilliseconds(190.0));
	}

	private static void PrepareCommunityOverlayContent(params FrameworkElement[] elements)
	{
		foreach (FrameworkElement element in elements)
		{
			element.BeginAnimation(UIElement.OpacityProperty, null);
			element.Opacity = 0.0;
		}
	}

	private static void ResetCommunityOverlayContent(params FrameworkElement[] elements)
	{
		foreach (FrameworkElement element in elements)
		{
			element.BeginAnimation(UIElement.OpacityProperty, null);
			element.Opacity = 1.0;
		}
	}

	private static void BeginCommunityOverlayContentFade(FrameworkElement element, TimeSpan beginTime)
	{
		element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(115.0))
		{
			BeginTime = beginTime,
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
	}

	private void RememberCommunitySwipeDirection(int direction)
	{
		if ((_viewModel.IsFriendsListScreen && direction < 0) || (_viewModel.IsPartyScreen && direction > 0))
		{
			_pendingCommunitySwipeDirection = direction;
		}
	}

	private double GetCommunityOverlayStartX(double fallback)
	{
		if (_pendingCommunitySwipeDirection == 0)
		{
			return fallback;
		}
		return (_pendingCommunitySwipeDirection < 0) ? (-170) : 170;
	}

	private static void BeginCommunityTabStripAnimation(FrameworkElement tabStrip, FrameworkElement activeTab, TranslateTransform activeTabOffset, int direction)
	{
		tabStrip.BeginAnimation(UIElement.OpacityProperty, null);
		tabStrip.Opacity = 0.96;
		tabStrip.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(210.0))
		{
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
		activeTab.BeginAnimation(UIElement.OpacityProperty, null);
		activeTabOffset.BeginAnimation(TranslateTransform.XProperty, null);
		if (direction == 0)
		{
			activeTab.Opacity = 1.0;
			activeTabOffset.X = 0.0;
			return;
		}
		activeTab.Opacity = 1.0;
		activeTabOffset.X = ((direction > 0) ? (-130) : 130);
		activeTab.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(260.0))
		{
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
		activeTabOffset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(290.0))
		{
			EasingFunction = new QuarticEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
	}

	private void BeginBladeTransition(int direction)
	{
		if (direction != 0)
		{
			GuideBladePanel.BeginAnimation(UIElement.OpacityProperty, null);
			GuideBladeOffset.BeginAnimation(TranslateTransform.XProperty, null);
			GuideBladePanel.Opacity = 0.92;
			GuideBladeOffset.X = ((direction > 0) ? 26 : (-26));
			GuideBladePanel.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(130.0))
			{
				EasingFunction = new SineEase
				{
					EasingMode = EasingMode.EaseOut
				}
			});
			GuideBladeOffset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(155.0))
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			});
		}
	}

	private static void AnimateSelectedListItem(ListBoxItem? item, int selectedIndex, ref int lastAnimatedIndex)
	{
		if (item != null && selectedIndex != lastAnimatedIndex)
		{
			lastAnimatedIndex = selectedIndex;
			AnimateFocusNudge(item);
		}
	}

	private static void AnimateSelectedButton(Button? button, int selectedIndex, ref int lastAnimatedIndex)
	{
		if (button != null && selectedIndex != lastAnimatedIndex)
		{
			lastAnimatedIndex = selectedIndex;
			AnimateFocusNudge(button);
		}
	}

	private static void AnimateFocusNudge(UIElement element)
	{
		TranslateTransform translateTransform = element.RenderTransform as TranslateTransform;
		if (translateTransform == null)
		{
			translateTransform = (TranslateTransform)(element.RenderTransform = new TranslateTransform());
		}
		element.BeginAnimation(UIElement.OpacityProperty, null);
		translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
		element.Opacity = 0.96;
		translateTransform.X = 5.0;
		element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(85.0))
		{
			EasingFunction = new SineEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
		translateTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(105.0))
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
	}

	private void LeftOuterTab_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		_viewModel.SelectRelativeTab(-2);
		FocusGuideMenu();
	}

	private void LeftInnerTab_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		_viewModel.SelectRelativeTab(-1);
		FocusGuideMenu();
	}

	private void RightInnerTab_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		_viewModel.SelectRelativeTab(1);
		FocusGuideMenu();
	}

	private void RightOuterTab_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		_viewModel.SelectRelativeTab(2);
		FocusGuideMenu();
	}

	private void FriendsPartyTab_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		RememberCommunitySwipeDirection(-1);
		if (_viewModel.SwitchCommunityTab(-1))
		{
			e.Handled = true;
			FocusGuideMenu();
		}
	}

	private void PartyFriendsTab_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		RememberCommunitySwipeDirection(1);
		if (_viewModel.SwitchCommunityTab(1))
		{
			e.Handled = true;
			FocusGuideMenu();
		}
	}

	private void GuideMenu_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		object originalSource = e.OriginalSource;
		ListBoxItem listBoxItem = FindAncestor<ListBoxItem>((DependencyObject?)((originalSource is DependencyObject) ? originalSource : null));
		if (listBoxItem != null)
		{
			GuideMenu.SelectedItem = listBoxItem.DataContext;
			_viewModel.ActivateSelected();
			FocusGuideMenu();
		}
	}

	private void FriendsList_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		object originalSource = e.OriginalSource;
		if (FindAncestor<ListBoxItem>((DependencyObject?)((originalSource is DependencyObject) ? originalSource : null))?.DataContext is GuideFriendListItem guideFriendListItem)
		{
			if (sender is ListBox listBox)
			{
				listBox.SelectedItem = guideFriendListItem;
			}
			_viewModel.ActivateFriendListItem(guideFriendListItem);
			FocusGuideMenu();
		}
	}

	private void FriendProfileActions_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		object originalSource = e.OriginalSource;
		if (FindAncestor<ListBoxItem>((DependencyObject?)((originalSource is DependencyObject) ? originalSource : null))?.DataContext is GuideMenuItem guideMenuItem)
		{
			FriendProfileOverlayActionsList.SelectedItem = guideMenuItem;
			_viewModel.ActivateFriendProfileAction(guideMenuItem);
			FocusGuideMenu();
		}
	}

	private void PartyRows_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		object originalSource = e.OriginalSource;
		if (FindAncestor<ListBoxItem>((DependencyObject?)((originalSource is DependencyObject) ? originalSource : null))?.DataContext is GuidePartyRowItem { IsSelectable: not false } guidePartyRowItem)
		{
			PartyOverlayListBox.SelectedItem = guidePartyRowItem;
			_viewModel.ActivatePartyRowItem(guidePartyRowItem);
			FocusGuideMenu();
		}
	}

	private void AchievementGames_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		object originalSource = e.OriginalSource;
		if (FindAncestor<ListBoxItem>((DependencyObject?)((originalSource is DependencyObject) ? originalSource : null))?.DataContext is GuideAchievementGameItem guideAchievementGameItem)
		{
			AchievementsGameListBox.SelectedItem = guideAchievementGameItem;
			_viewModel.ActivateAchievementGameItem(guideAchievementGameItem);
			FocusGuideMenu();
		}
	}

	private void Achievements_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		object originalSource = e.OriginalSource;
		if (FindAncestor<ListBoxItem>((DependencyObject?)((originalSource is DependencyObject) ? originalSource : null))?.DataContext is GuideAchievementItem guideAchievementItem)
		{
			AchievementsOverlayListBox.SelectedItem = guideAchievementItem;
			_viewModel.SelectAchievementItem(guideAchievementItem);
			FocusGuideMenu();
		}
	}

	private void DisableMouseWheelScroll(object sender, MouseWheelEventArgs e)
	{
		e.Handled = true;
	}

	private void MediaSubmenu_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		object originalSource = e.OriginalSource;
		ListBoxItem listBoxItem = FindAncestor<ListBoxItem>((DependencyObject?)((originalSource is DependencyObject) ? originalSource : null));
		if (listBoxItem != null)
		{
			MediaSubmenuList.SelectedItem = listBoxItem.DataContext;
			_viewModel.ActivateSelected();
			FocusGuideMenu();
		}
	}

	private void GuideMusicPicker_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
	}

	private void GuideMusicFullscreenHint_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (_viewModel.Dashboard.OpenMusicVisualizerFullscreenCommand.CanExecute(null))
		{
			_viewModel.Dashboard.OpenMusicVisualizerFullscreenCommand.Execute(null);
		}
		e.Handled = true;
	}

	private void MinimizeDashboardHint_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		_viewModel.PlaySound("guide-select");
		_viewModel.MinimizeDashboard();
		e.Handled = true;
	}

	private void FooterXHint_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		_viewModel.HandleFooterX();
		e.Handled = true;
		FocusGuideMenu();
	}

	protected override void OnClosed(EventArgs e)
	{
		_viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
		base.OnClosed(e);
	}

	private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
	{
		while (current != null)
		{
			T val = (T)(object)((current is T) ? current : null);
			if (val != null)
			{
				return val;
			}
			current = VisualTreeHelper.GetParent(current);
		}
		return default(T);
	}

	private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
	{
		if (parent == null)
		{
			return default(T);
		}
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			T val = (T)(object)((child is T) ? child : null);
			if (val != null)
			{
				return val;
			}
			T val2 = FindVisualChild<T>(child);
			if (val2 != null)
			{
				return val2;
			}
		}
		return default(T);
	}

	private bool TryMoveGuideMusicFocus(DashboardInputAction action)
	{
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		List<OverlayFocusCandidate> overlayFocusCandidates = GetOverlayFocusCandidates(GuideMusicOverlay);
		if (overlayFocusCandidates.Count == 0)
		{
			return false;
		}
		IInputElement focusedElement = Keyboard.FocusedElement;
		Control currentControl = focusedElement as Control;
		if (currentControl == null || !overlayFocusCandidates.Any((OverlayFocusCandidate candidate) => candidate.Control == currentControl))
		{
			return TryFocus(overlayFocusCandidates[0].Control);
		}
		Point currentCenter = GetCenter(overlayFocusCandidates.First((OverlayFocusCandidate candidate) => candidate.Control == currentControl).Bounds);
		Vector direction = (Vector)(action switch
		{
			DashboardInputAction.MoveLeft => new Vector(-1.0, 0.0), 
			DashboardInputAction.MoveRight => new Vector(1.0, 0.0), 
			DashboardInputAction.MoveUp => new Vector(0.0, -1.0), 
			DashboardInputAction.MoveDown => new Vector(0.0, 1.0), 
			_ => new Vector(0.0, 0.0), 
		});
		var anon = (from item in (from candidate in overlayFocusCandidates
				where candidate.Control != currentControl
				select new
				{
					Candidate = candidate,
					Center = GetCenter(candidate.Bounds)
				}).Select(item =>
			{
				//IL_0008: Unknown result type (might be due to invalid IL or missing references)
				//IL_000e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0013: Unknown result type (might be due to invalid IL or missing references)
				//IL_0018: Unknown result type (might be due to invalid IL or missing references)
				//IL_0052: Unknown result type (might be due to invalid IL or missing references)
				//IL_0057: Unknown result type (might be due to invalid IL or missing references)
				//IL_0030: Unknown result type (might be due to invalid IL or missing references)
				//IL_0035: Unknown result type (might be due to invalid IL or missing references)
				//IL_0088: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
				//IL_008f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0094: Unknown result type (might be due to invalid IL or missing references)
				OverlayFocusCandidate candidate = item.Candidate;
				Vector delta = item.Center - currentCenter;
				DashboardInputAction dashboardInputAction = action;
				Point center;
				double num;
				if ((uint)dashboardInputAction > 1u)
				{
					center = item.Center;
					num = Math.Abs(center.Y - currentCenter.Y);
				}
				else
				{
					center = item.Center;
					num = Math.Abs(center.X - currentCenter.X);
				}
				double primary = num;
				dashboardInputAction = action;
				double secondary;
				if (!((uint)dashboardInputAction <= 1u))
				{
					center = item.Center;
					secondary = Math.Abs(center.X - currentCenter.X);
				}
				else
				{
					center = item.Center;
					secondary = Math.Abs(center.Y - currentCenter.Y);
				}
				return new
				{
					Candidate = candidate,
					Delta = delta,
					Primary = primary,
					Secondary = secondary
				};
			})
			where Vector.Multiply(item.Delta, direction) > 1.0
			orderby item.Secondary * 2.2 + item.Primary, item.Primary
			select item).FirstOrDefault();
		if (anon != null)
		{
			return TryFocus(anon.Candidate.Control);
		}
		return false;
	}

	private void ActivateFocusedGuideMusicControl()
	{
		if (!(Keyboard.FocusedElement is Button button))
		{
			return;
		}
		if (button.Command != null)
		{
			if (button.Command.CanExecute(button.CommandParameter))
			{
				button.Command.Execute(button.CommandParameter);
			}
		}
		else
		{
			button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
		}
	}

	private static List<OverlayFocusCandidate> GetOverlayFocusCandidates(FrameworkElement overlay)
	{
		try
		{
			return (from control in FindVisualChildren<Control>((DependencyObject?)(object)overlay)
				where control.IsVisible && control.IsEnabled && control.Focusable
				select new OverlayFocusCandidate(control, GetElementBounds(control, overlay))).Where(delegate(OverlayFocusCandidate candidate)
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_001c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0021: Unknown result type (might be due to invalid IL or missing references)
				Rect bounds = candidate.Bounds;
				if (bounds.Width > 0.0)
				{
					bounds = candidate.Bounds;
					return bounds.Height > 0.0;
				}
				return false;
			}).Where(delegate(OverlayFocusCandidate candidate)
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				Point center = GetCenter(candidate.Bounds);
				return center.X >= 0.0 && center.Y >= 0.0 && center.X <= overlay.ActualWidth && center.Y <= overlay.ActualHeight;
			}).ToList();
		}
		catch
		{
			return new List<OverlayFocusCandidate>();
		}
	}

	private static Rect GetElementBounds(FrameworkElement element, Visual relativeTo)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			return element.TransformToAncestor(relativeTo).TransformBounds(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight));
		}
		catch (InvalidOperationException)
		{
			return Rect.Empty;
		}
		catch (ArgumentException)
		{
			return Rect.Empty;
		}
		catch
		{
			return Rect.Empty;
		}
	}

	private static Point GetCenter(Rect rect)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		return new Point(rect.Left + rect.Width / 2.0, rect.Top + rect.Height / 2.0);
	}

	private static UIElement? FindFocusableControl(DependencyObject? root)
	{
		return FindVisualChildren<UIElement>(root).FirstOrDefault((UIElement element) => element.IsVisible && element.Focusable && (!(element is Control control) || control.IsEnabled));
	}

	private bool TryFocus(UIElement? element)
	{
		if (element == null || !element.IsVisible || !element.Focusable)
		{
			return false;
		}
		if (element is Control && !element.IsEnabled)
		{
			return false;
		}
		try
		{
			return element.Focus();
		}
		catch
		{
			return false;
		}
	}

	private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? parent) where T : DependencyObject
	{
		if (parent == null)
		{
			yield break;
		}
		int count = VisualTreeHelper.GetChildrenCount(parent);
		for (int index = 0; index < count; index++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, index);
			T val = (T)(object)((child is T) ? child : null);
			if (val != null)
			{
				yield return val;
			}
			foreach (T item in FindVisualChildren<T>(child))
			{
				yield return item;
			}
		}
	}

	private static bool IsPhysicalTypingKey(Key key)
	{
		return key == Key.Space ||
			(key >= Key.D0 && key <= Key.Z) ||
			(key >= Key.NumPad0 && key <= Key.Divide) ||
			(key >= Key.Oem1 && key <= Key.Oem102);
	}

	private Button? FindSearchKeyButton(int index)
	{
		if (index < 0)
		{
			return null;
		}
		if (index < 40)
		{
			FriendSearchOverlayMainKeysItems.UpdateLayout();
			return FindVisualChild<Button>(FriendSearchOverlayMainKeysItems.ItemContainerGenerator.ContainerFromIndex(index));
		}
		return index switch
		{
			40 => FriendSearchCapsButton, 
			41 => FriendSearchBackspaceButton, 
			42 => FriendSearchSpaceButton, 
			43 => FriendSearchDoneButton, 
			_ => null, 
		};
	}
}
