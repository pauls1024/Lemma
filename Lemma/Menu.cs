﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ComponentBind;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;

namespace Lemma.Components
{
	public class Menu : Component<GameMain>, IUpdateableComponent
	{
		private static Dictionary<string, string> maps = new Dictionary<string,string>
		{
#if DEBUG
			{ "test", "Test Level" },
#endif
			{ "sandbox", "Sandbox" },
			{ "start", "\\map apartment" },
			{ "rain", "\\map rain" },
			{ "dawn", "\\map dawn" },
			{ "monolith", "\\map monolith" },
			{ "forest", "\\map forest" },
			{ "valley", "\\map valley" },
		};

		private const float messageFadeTime = 0.75f;
		private const float messageBackgroundOpacity = 0.75f;

		private const float menuButtonWidth = 256.0f;
		private const float menuButtonSettingOffset = 180.0f; // Horizontal offset for the value label on a settings menu item
		private const float menuButtonLeftPadding = 40.0f;
		private const float animationSpeed = 2.5f;
		private const float hideAnimationSpeed = 5.0f;

		private static Color highlightColor = new Color(0.0f, 0.175f, 0.35f);

		private List<Property<PCInput.PCInputBinding>> inputBindings = new List<Property<PCInput.PCInputBinding>>();

		private ListContainer messages;

		private PCInput input;

		public string Credits;

		private int displayModeIndex;

		private DisplayModeCollection supportedDisplayModes;

		Property<UIComponent> currentMenu = new Property<UIComponent> { Value = null };

		// Settings to be restored when unpausing
		private float originalBlurAmount = 0.0f;
		private bool originalMouseVisible = false;
		private Point originalMousePosition = new Point();

		public Menu()
		{
		}

		public void ClearMessages()
		{
			this.messages.Children.Clear();
		}

		private Container buildMessage()
		{
			Container msgBackground = new Container();

			this.messages.Children.Add(msgBackground);

			msgBackground.Tint.Value = Color.Black;
			msgBackground.Opacity.Value = messageBackgroundOpacity;
			TextElement msg = new TextElement();
			msg.FontFile.Value = "Font";
			msg.WrapWidth.Value = 250.0f;
			msgBackground.Children.Add(msg);
			return msgBackground;
		}

		public Container ShowMessage(Entity entity, Func<string> text, params IProperty[] properties)
		{
			Container container = this.buildMessage();
			TextElement textElement = (TextElement)container.Children[0];
			textElement.Add(new Binding<string>(textElement.Text, text, properties));

			this.animateMessage(entity, container);

			return container;
		}

		private void animateMessage(Entity entity, Container container)
		{
			container.CheckLayout();
			Vector2 originalSize = container.Size;
			container.ResizeVertical.Value = false;
			container.EnableScissor.Value = true;
			container.Size.Value = new Vector2(originalSize.X, 0);

			Animation anim = new Animation
			(
				new Animation.Ease(new Animation.Vector2MoveTo(container.Size, originalSize, messageFadeTime), Animation.Ease.Type.OutExponential),
				new Animation.Set<bool>(container.ResizeVertical, true)
			);

			if (entity == null)
			{
				anim.EnabledWhenPaused.Value = false;
				this.main.AddComponent(anim);
			}
			else
				entity.Add(anim);
		}

		public Container ShowMessage(Entity entity, string text)
		{
			Container container = this.buildMessage();
			TextElement textElement = (TextElement)container.Children[0];
			textElement.Text.Value = text;

			this.animateMessage(entity, container);

			return container;
		}

		public void HideMessage(Entity entity, Container container, float delay = 0.0f)
		{
			if (container != null && container.Active)
			{
				container.CheckLayout();
				Animation anim = new Animation
				(
					new Animation.Delay(delay),
					new Animation.Set<bool>(container.ResizeVertical, false),
					new Animation.Ease(new Animation.Vector2MoveTo(container.Size, new Vector2(container.Size.Value.X, 0), messageFadeTime), Animation.Ease.Type.OutExponential),
					new Animation.Execute(container.Delete)
				);

				if (entity == null)
				{
					anim.EnabledWhenPaused.Value = false;
					this.main.AddComponent(anim);
				}
				else
					entity.Add(anim);
			}
		}

		private UIComponent createMenuButton<Type>(string label, Property<Type> property)
		{
			return this.createMenuButton<Type>(label, property, x => x.ToString());
		}

		private UIComponent createMenuButton<Type>(string label, Property<Type> property, Func<Type, string> conversion)
		{
			UIComponent result = this.CreateButton();

			TextElement text = new TextElement();
			text.Name.Value = "Text";
			text.FontFile.Value = "Font";
			text.Text.Value = label;
			result.Children.Add(text);

			TextElement value = new TextElement();
			value.Position.Value = new Vector2(Menu.menuButtonSettingOffset, value.Position.Value.Y);
			value.Name.Value = "Value";
			value.FontFile.Value = "Font";
			value.Add(new Binding<string, Type>(value.Text, conversion, property));
			result.Children.Add(value);

			return result;
		}

		public Container CreateContainer(bool autosize = false)
		{
			Container result = new Container();
			result.Tint.Value = Color.Black;
			if (!autosize)
			{
				result.ResizeHorizontal.Value = false;
				result.Size.Value = new Vector2(Menu.menuButtonWidth + Menu.menuButtonLeftPadding + 4.0f, 0.0f);
				result.PaddingLeft.Value = menuButtonLeftPadding;
			}
			return result;
		}

		public UIComponent CreateButton(Action action = null, bool autosize = false)
		{
			Container result = this.CreateContainer(autosize);

			result.Add(new Binding<Color, bool>(result.Tint, x => x ? Menu.highlightColor : new Color(0.0f, 0.0f, 0.0f), result.Highlighted));
			result.Add(new Binding<float, bool>(result.Opacity, x => x ? 1.0f : 0.5f, result.Highlighted));
			result.Add(new NotifyBinding(delegate()
			{
				if (result.Highlighted)
					AkSoundEngine.PostEvent(AK.EVENTS.PLAY_UI_MOUSEOVER);
			}, result.Highlighted));
			result.Add(new CommandBinding<Point>(result.MouseLeftUp, delegate(Point p)
			{
				AkSoundEngine.PostEvent(AK.EVENTS.PLAY_UI_CLICK);
				if (action != null)
					action();
			}));
			return result;
		}

		public UIComponent CreateButton(string label, Action action = null, bool autosize = false)
		{
			UIComponent result = this.CreateButton(action, autosize);
			TextElement text = new TextElement();
			text.Name.Value = "Text";
			text.FontFile.Value = "Font";
			text.Text.Value = label;
			result.Children.Add(text);

			return result;
		}

		public void RemoveSaveGame(string filename)
		{
			UIComponent container = this.loadSaveList.Children.FirstOrDefault(x => ((string)x.UserData.Value) == filename);
			if (container != null)
				container.Delete.Execute();
		}

		public void AddSaveGame(string timestamp)
		{
			GameMain.SaveInfo info = null;
			try
			{
				using (Stream stream = new FileStream(Path.Combine(this.main.SaveDirectory, timestamp, "save.xml"), FileMode.Open, FileAccess.Read, FileShare.None))
					info = (GameMain.SaveInfo)new XmlSerializer(typeof(GameMain.SaveInfo)).Deserialize(stream);
				if (info.Version != GameMain.MapVersion)
					throw new Exception();
			}
			catch (Exception) // Old version. Delete it.
			{
				string savePath = Path.Combine(this.main.SaveDirectory, timestamp);
				if (Directory.Exists(savePath))
				{
					try
					{
						Directory.Delete(savePath, true);
					}
					catch (Exception)
					{
						// Whatever. We can't delete it, tough beans.
					}
				}
				return;
			}

			UIComponent container = this.CreateButton();
			container.UserData.Value = timestamp;

			ListContainer layout = new ListContainer();
			layout.Orientation.Value = ListContainer.ListOrientation.Vertical;
			container.Children.Add(layout);

			Sprite sprite = new Sprite();
			sprite.IsStandardImage.Value = true;
			sprite.Image.Value = Path.Combine(this.main.SaveDirectory, timestamp, "thumbnail.jpg");
			layout.Children.Add(sprite);

			TextElement label = new TextElement();
			label.FontFile.Value = "Font";
			label.Text.Value = timestamp;
			layout.Children.Add(label);

			container.Add(new CommandBinding<Point>(container.MouseLeftUp, delegate(Point p)
			{
				if (this.saveMode)
				{
					this.loadSaveMenu.EnableInput.Value = false;
					this.ShowDialog("\\overwrite prompt", "\\overwrite", delegate()
					{
						container.Delete.Execute();
						this.main.SaveOverwrite(timestamp);
						this.hideLoadSave();
						this.main.Paused.Value = false;
						this.restorePausedSettings();
					});
				}
				else
				{
					this.hideLoadSave();
					this.main.Paused.Value = false;
					this.restorePausedSettings();
					this.main.CurrentSave.Value = timestamp;
					this.main.MapFile.Value = info.MapFile;
				}
			}));

			this.loadSaveList.Children.Add(container);
			this.loadSaveScroll.ScrollToTop();
		}

		private ListContainer pauseMenu;
		private ListContainer notifications;

		private ListContainer loadSaveMenu;
		private ListContainer loadSaveList;
		private Scroller loadSaveScroll;
		private bool loadSaveShown;
		private Animation loadSaveAnimation;
		private Property<bool> saveMode = new Property<bool> { Value = false };

		private Animation pauseAnimation;
		private Container dialog;

		private void hidePauseMenu()
		{
			if (this.pauseAnimation != null)
				this.pauseAnimation.Delete.Execute();
			this.pauseAnimation = new Animation
			(
				new Animation.Vector2MoveToSpeed(this.pauseMenu.AnchorPoint, new Vector2(1, 0.5f), Menu.hideAnimationSpeed),
				new Animation.Set<bool>(this.pauseMenu.Visible, false)
			);
			this.main.AddComponent(this.pauseAnimation);
			this.currentMenu.Value = null;
		}

		private void showPauseMenu()
		{
			this.pauseMenu.Visible.Value = true;
			if (this.pauseAnimation != null)
				this.pauseAnimation.Delete.Execute();
			this.pauseAnimation = new Animation(new Animation.Ease(new Animation.Vector2MoveToSpeed(this.pauseMenu.AnchorPoint, new Vector2(0, 0.5f), Menu.animationSpeed), Animation.Ease.Type.OutExponential));
			this.main.AddComponent(this.pauseAnimation);
			this.currentMenu.Value = this.pauseMenu;
		}

		// Pause
		private void savePausedSettings()
		{
			// Take screenshot
			this.main.Screenshot.Take();

			this.originalMouseVisible = this.main.IsMouseVisible;
			this.main.IsMouseVisible.Value = true;
			this.originalBlurAmount = this.main.Renderer.BlurAmount;

			// Save mouse position
			MouseState mouseState = this.main.MouseState;
			this.originalMousePosition = new Point(mouseState.X, mouseState.Y);

			this.pauseMenu.Visible.Value = true;
			this.pauseMenu.AnchorPoint.Value = new Vector2(1, 0.5f);

			// Blur the screen and show the pause menu
			if (this.pauseAnimation != null && this.pauseAnimation.Active)
				this.pauseAnimation.Delete.Execute();

			this.pauseAnimation = new Animation
			(
				new Animation.Parallel
				(
					new Animation.FloatMoveToSpeed(this.main.Renderer.BlurAmount, 1.0f, 1.0f),
					new Animation.Ease(new Animation.Vector2MoveToSpeed(this.pauseMenu.AnchorPoint, new Vector2(0, 0.5f), Menu.animationSpeed), Animation.Ease.Type.OutExponential)
				)
			);
			this.main.AddComponent(this.pauseAnimation);

			this.currentMenu.Value = this.pauseMenu;

			if (this.main.MapFile.Value != GameMain.MenuMap)
			{
				// TODO: XACT -> Wwise
				//this.AudioEngine.GetCategory("Default").Pause();
			}
		}

		// Unpause
		private void restorePausedSettings()
		{
			if (this.pauseAnimation != null && this.pauseAnimation.Active)
				this.pauseAnimation.Delete.Execute();

			// Restore mouse
			if (!originalMouseVisible)
			{
				// Only restore mouse position if the cursor was not visible
				// i.e., we're in first-person camera mode
				Microsoft.Xna.Framework.Input.Mouse.SetPosition(originalMousePosition.X, originalMousePosition.Y);
				MouseState m = new MouseState(originalMousePosition.X, originalMousePosition.Y, this.main.MouseState.Value.ScrollWheelValue, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
				this.main.LastMouseState.Value = m;
				this.main.MouseState.Value = m;
			}
			this.main.IsMouseVisible.Value = originalMouseVisible;

			this.main.SaveSettings();

			// Unlur the screen and show the pause menu
			if (this.pauseAnimation != null && this.pauseAnimation.Active)
				this.pauseAnimation.Delete.Execute();

			this.main.Renderer.BlurAmount.Value = originalBlurAmount;
			this.pauseAnimation = new Animation
			(
				new Animation.Parallel
				(
					new Animation.Sequence
					(
						new Animation.Vector2MoveToSpeed(this.pauseMenu.AnchorPoint, new Vector2(1, 0.5f), Menu.hideAnimationSpeed),
						new Animation.Set<bool>(this.pauseMenu.Visible, false)
					)
				)
			);
			this.main.AddComponent(this.pauseAnimation);

			this.main.Screenshot.Clear();

			this.currentMenu.Value = null;

			// TODO: XACT -> Wwise
			//this.AudioEngine.GetCategory("Default").Resume();
		}


		public void ShowDialog(string question, string action, Action callback)
		{
			if (this.dialog != null)
				this.dialog.Delete.Execute();
			this.dialog = new Container();
			this.dialog.Tint.Value = Color.Black;
			this.dialog.Opacity.Value = 0.5f;
			this.dialog.AnchorPoint.Value = new Vector2(0.5f);
			this.dialog.Add(new Binding<Vector2, Point>(this.dialog.Position, x => new Vector2(x.X * 0.5f, x.Y * 0.5f), this.main.ScreenSize));
			this.dialog.Add(new CommandBinding(this.dialog.Delete, delegate()
			{
				this.loadSaveMenu.EnableInput.Value = true;
			}));
			this.main.UI.Root.Children.Add(this.dialog);

			ListContainer dialogLayout = new ListContainer();
			dialogLayout.Orientation.Value = ListContainer.ListOrientation.Vertical;
			this.dialog.Children.Add(dialogLayout);

			TextElement prompt = new TextElement();
			prompt.FontFile.Value = "Font";
			prompt.Text.Value = question;
			dialogLayout.Children.Add(prompt);

			ListContainer dialogButtons = new ListContainer();
			dialogButtons.Orientation.Value = ListContainer.ListOrientation.Horizontal;
			dialogLayout.Children.Add(dialogButtons);

			UIComponent okay = this.CreateButton("", delegate()
			{
				this.dialog.Delete.Execute();
				this.dialog = null;
				callback();
			}, true);
			TextElement okayText = (TextElement)okay.GetChildByName("Text");
			okayText.Add(new Binding<string, bool>(okayText.Text, x => action + (x ? " gamepad" : ""), this.main.GamePadConnected));
			okay.Name.Value = "Okay";
			dialogButtons.Children.Add(okay);

			UIComponent cancel = this.CreateButton("\\cancel", delegate()
			{
				this.dialog.Delete.Execute();
				this.dialog = null;
			}, true);
			dialogButtons.Children.Add(cancel);

			TextElement cancelText = (TextElement)cancel.GetChildByName("Text");
			cancelText.Add(new Binding<string, bool>(cancelText.Text, x => x ? "\\cancel gamepad" : "\\cancel", this.main.GamePadConnected));
		}

		private void hideLoadSave()
		{
			this.showPauseMenu();

			if (this.dialog != null)
			{
				this.dialog.Delete.Execute();
				this.dialog = null;
			}

			this.loadSaveShown = false;

			if (this.loadSaveAnimation != null)
				this.loadSaveAnimation.Delete.Execute();
			this.loadSaveAnimation = new Animation
			(
				new Animation.Vector2MoveToSpeed(this.loadSaveMenu.AnchorPoint, new Vector2(1, 0.5f), Menu.hideAnimationSpeed),
				new Animation.Set<bool>(this.loadSaveMenu.Visible, false)
			);
			this.main.AddComponent(this.loadSaveAnimation);
		}

		public void SetupDisplayModes(DisplayModeCollection supportedDisplayModes, int displayModeIndex)
		{
			this.supportedDisplayModes = supportedDisplayModes;
			this.displayModeIndex = displayModeIndex;
		}

		public override void InitializeProperties()
		{
			base.InitializeProperties();

			this.input = new PCInput();
			this.main.AddComponent(this.input);

			// Toggle fullscreen
			this.input.Bind(this.main.Settings.ToggleFullscreen, PCInput.InputState.Down, delegate()
			{
				if (this.main.Graphics.IsFullScreen) // Already fullscreen. Go to windowed mode.
					this.main.ExitFullscreen();
				else // In windowed mode. Go to fullscreen.
					this.main.EnterFullscreen();
			});

#if DEBUG
			Log.Handler = delegate(string log)
			{
				this.HideMessage(null, this.ShowMessage(null, log), 2.0f);
			};
#endif

			// Message list
			this.messages = new ListContainer();
			this.messages.Alignment.Value = ListContainer.ListAlignment.Max;
			this.messages.AnchorPoint.Value = new Vector2(1.0f, 1.0f);
			this.messages.Reversed.Value = true;
			this.messages.Add(new Binding<Vector2, Point>(this.messages.Position, x => new Vector2(x.X * 0.9f, x.Y * 0.9f), this.main.ScreenSize));
			this.main.UI.Root.Children.Add(this.messages);

			this.notifications = new ListContainer();
			this.notifications.Alignment.Value = ListContainer.ListAlignment.Max;
			this.notifications.AnchorPoint.Value = new Vector2(1.0f, 0.0f);
			this.notifications.Name.Value = "Notifications";
			this.notifications.Add(new Binding<Vector2, Point>(this.notifications.Position, x => new Vector2(x.X * 0.9f, x.Y * 0.1f), this.main.ScreenSize));
			this.main.UI.Root.Children.Add(this.notifications);

			// Fullscreen message
			Container msgBackground = new Container();
			this.main.UI.Root.Children.Add(msgBackground);
			msgBackground.Tint.Value = Color.Black;
			msgBackground.Opacity.Value = 0.2f;
			msgBackground.AnchorPoint.Value = new Vector2(0.5f, 1.0f);
			msgBackground.Add(new Binding<Vector2, Point>(msgBackground.Position, x => new Vector2(x.X * 0.5f, x.Y - 30.0f), this.main.ScreenSize));
			TextElement msg = new TextElement();
			msg.FontFile.Value = "Font";
			msg.Text.Value = "\\toggle fullscreen tooltip";
			msgBackground.Children.Add(msg);
			this.main.AddComponent(new Animation
			(
				new Animation.Delay(4.0f),
				new Animation.Parallel
				(
					new Animation.FloatMoveTo(msgBackground.Opacity, 0.0f, 2.0f),
					new Animation.FloatMoveTo(msg.Opacity, 0.0f, 2.0f)
				),
				new Animation.Execute(delegate() { this.main.UI.Root.Children.Remove(msgBackground); })
			));

			// Pause menu
			this.pauseMenu = new ListContainer();
			this.pauseMenu.Visible.Value = false;
			this.pauseMenu.Add(new Binding<Vector2, Point>(this.pauseMenu.Position, x => new Vector2(0, x.Y * 0.5f), this.main.ScreenSize));
			this.pauseMenu.AnchorPoint.Value = new Vector2(1, 0.5f);
			this.main.UI.Root.Children.Add(this.pauseMenu);
			this.pauseMenu.Orientation.Value = ListContainer.ListOrientation.Vertical;

			// Load / save menu
			this.loadSaveMenu = new ListContainer();
			this.loadSaveMenu.Visible.Value = false;
			this.loadSaveMenu.Add(new Binding<Vector2, Point>(this.loadSaveMenu.Position, x => new Vector2(0, x.Y * 0.5f), this.main.ScreenSize));
			this.loadSaveMenu.AnchorPoint.Value = new Vector2(1, 0.5f);
			this.loadSaveMenu.Orientation.Value = ListContainer.ListOrientation.Vertical;
			this.main.UI.Root.Children.Add(this.loadSaveMenu);

			Container loadSavePadding = this.CreateContainer();
			this.loadSaveMenu.Children.Add(loadSavePadding);

			ListContainer loadSaveLabelContainer = new ListContainer();
			loadSaveLabelContainer.Orientation.Value = ListContainer.ListOrientation.Vertical;
			loadSavePadding.Children.Add(loadSaveLabelContainer);

			TextElement loadSaveLabel = new TextElement();
			loadSaveLabel.FontFile.Value = "Font";
			loadSaveLabel.Add(new Binding<string, bool>(loadSaveLabel.Text, x => x ? "S A V E" : "L O A D", this.saveMode));
			loadSaveLabelContainer.Children.Add(loadSaveLabel);

			TextElement loadSaveScrollLabel = new TextElement();
			loadSaveScrollLabel.FontFile.Value = "Font";
			loadSaveScrollLabel.Text.Value = "\\scroll for more";
			loadSaveLabelContainer.Children.Add(loadSaveScrollLabel);

			TextElement quickSaveLabel = new TextElement();
			quickSaveLabel.FontFile.Value = "Font";
			quickSaveLabel.Add(new Binding<bool>(quickSaveLabel.Visible, this.saveMode));
			quickSaveLabel.Text.Value = "\\quicksave instructions";
			loadSaveLabelContainer.Children.Add(quickSaveLabel);

			UIComponent loadSaveBack = this.CreateButton("\\back", this.hideLoadSave);
			this.loadSaveMenu.Children.Add(loadSaveBack);

			UIComponent saveNewButton = this.CreateButton("\\save new", delegate()
			{
				this.main.SaveOverwrite();
				this.hideLoadSave();
				this.main.Paused.Value = false;
				this.restorePausedSettings();
			});
			saveNewButton.Add(new Binding<bool>(saveNewButton.Visible, this.saveMode));
			this.loadSaveMenu.Children.Add(saveNewButton);

			this.loadSaveScroll = new Scroller();
			this.loadSaveScroll.Add(new Binding<Vector2, Point>(this.loadSaveScroll.Size, x => new Vector2(Menu.menuButtonWidth + Menu.menuButtonLeftPadding + 4.0f, x.Y * 0.5f), this.main.ScreenSize));
			this.loadSaveMenu.Children.Add(this.loadSaveScroll);

			this.loadSaveList = new ListContainer();
			this.loadSaveList.Orientation.Value = ListContainer.ListOrientation.Vertical;
			this.loadSaveList.Reversed.Value = true;
			this.loadSaveScroll.Children.Add(this.loadSaveList);

			foreach (string saveFile in Directory.GetDirectories(this.main.SaveDirectory, "*", SearchOption.TopDirectoryOnly).Select(x => Path.GetFileName(x)).OrderBy(x => x))
				this.AddSaveGame(saveFile);

			// Settings menu
			bool settingsShown = false;
			Animation settingsAnimation = null;

			Func<bool, string> boolDisplay = x => x ? "\\on" : "\\off";

			ListContainer settingsMenu = new ListContainer();
			settingsMenu.Visible.Value = false;
			settingsMenu.Add(new Binding<Vector2, Point>(settingsMenu.Position, x => new Vector2(0, x.Y * 0.5f), this.main.ScreenSize));
			settingsMenu.AnchorPoint.Value = new Vector2(1, 0.5f);
			this.main.UI.Root.Children.Add(settingsMenu);
			settingsMenu.Orientation.Value = ListContainer.ListOrientation.Vertical;

			Container settingsLabelPadding = this.CreateContainer();
			settingsMenu.Children.Add(settingsLabelPadding);

			ListContainer settingsLabelContainer = new ListContainer();
			settingsLabelContainer.Orientation.Value = ListContainer.ListOrientation.Vertical;
			settingsLabelPadding.Children.Add(settingsLabelContainer);

			TextElement settingsLabel = new TextElement();
			settingsLabel.FontFile.Value = "Font";
			settingsLabel.Text.Value = "\\options title";
			settingsLabelContainer.Children.Add(settingsLabel);

			TextElement settingsScrollLabel = new TextElement();
			settingsScrollLabel.FontFile.Value = "Font";
			settingsScrollLabel.Add(new Binding<string>(settingsScrollLabel.Text, delegate()
			{
				if (this.main.GamePadConnected)
					return "\\modify setting gamepad";
				else
					return "\\modify setting";
			}, this.main.GamePadConnected));
			settingsLabelContainer.Children.Add(settingsScrollLabel);

			Action hideSettings = delegate()
			{
				this.showPauseMenu();

				settingsShown = false;

				if (settingsAnimation != null)
					settingsAnimation.Delete.Execute();
				settingsAnimation = new Animation
				(
					new Animation.Vector2MoveToSpeed(settingsMenu.AnchorPoint, new Vector2(1, 0.5f), Menu.hideAnimationSpeed),
					new Animation.Set<bool>(settingsMenu.Visible, false)
				);
				this.main.AddComponent(settingsAnimation);
			};

			UIComponent settingsBack = this.CreateButton("\\back", hideSettings);
			settingsMenu.Children.Add(settingsBack);

			UIComponent fullscreenResolution = this.createMenuButton<Point>("\\fullscreen resolution", this.main.Settings.FullscreenResolution, x => x.X.ToString() + "x" + x.Y.ToString());
			
			Action<int> changeFullscreenResolution = delegate(int scroll)
			{
				displayModeIndex = (displayModeIndex + scroll) % this.supportedDisplayModes.Count();
				while (displayModeIndex < 0)
					displayModeIndex += this.supportedDisplayModes.Count();
				DisplayMode mode = this.supportedDisplayModes.ElementAt(displayModeIndex);
				this.main.Settings.FullscreenResolution.Value = new Point(mode.Width, mode.Height);
			};

			fullscreenResolution.Add(new CommandBinding<Point>(fullscreenResolution.MouseLeftUp, delegate(Point mouse)
			{
				changeFullscreenResolution(1);
			}));
			fullscreenResolution.Add(new CommandBinding<Point, int>(fullscreenResolution.MouseScrolled, delegate(Point mouse, int scroll)
			{
				changeFullscreenResolution(scroll);
			}));
			settingsMenu.Children.Add(fullscreenResolution);

			UIComponent vsyncEnabled = this.createMenuButton<bool>("\\vsync", this.main.Settings.EnableVsync, boolDisplay);
			vsyncEnabled.Add(new CommandBinding<Point, int>(vsyncEnabled.MouseScrolled, delegate(Point mouse, int scroll)
			{
				this.main.Settings.EnableVsync.Value = !this.main.Settings.EnableVsync;
			}));
			vsyncEnabled.Add(new CommandBinding<Point>(vsyncEnabled.MouseLeftUp, delegate(Point mouse)
			{
				this.main.Settings.EnableVsync.Value = !this.main.Settings.EnableVsync;
			}));
			settingsMenu.Children.Add(vsyncEnabled);

			UIComponent gamma = this.createMenuButton<float>("\\gamma", this.main.Renderer.Gamma, x => ((int)Math.Round(x * 100.0f)).ToString() + "%");
			gamma.Add(new CommandBinding<Point, int>(gamma.MouseScrolled, delegate(Point mouse, int scroll)
			{
				this.main.Renderer.Gamma.Value = Math.Max(0, Math.Min(2, this.main.Renderer.Gamma + (scroll * 0.1f)));
			}));
			settingsMenu.Children.Add(gamma);

			UIComponent fieldOfView = this.createMenuButton<float>("\\field of view", this.main.Camera.FieldOfView, x => ((int)Math.Round(MathHelper.ToDegrees(this.main.Camera.FieldOfView))).ToString() + "°");
			fieldOfView.Add(new CommandBinding<Point, int>(fieldOfView.MouseScrolled, delegate(Point mouse, int scroll)
			{
				this.main.Camera.FieldOfView.Value = Math.Max(MathHelper.ToRadians(60.0f), Math.Min(MathHelper.ToRadians(120.0f), this.main.Camera.FieldOfView + MathHelper.ToRadians(scroll)));
			}));
			settingsMenu.Children.Add(fieldOfView);

			UIComponent motionBlurAmount = this.createMenuButton<float>("\\motion blur amount", this.main.Renderer.MotionBlurAmount, x => ((int)Math.Round(x * 100.0f)).ToString() + "%");
			motionBlurAmount.Add(new CommandBinding<Point, int>(motionBlurAmount.MouseScrolled, delegate(Point mouse, int scroll)
			{
				this.main.Renderer.MotionBlurAmount.Value = Math.Max(0, Math.Min(1, this.main.Renderer.MotionBlurAmount + (scroll * 0.1f)));
			}));
			settingsMenu.Children.Add(motionBlurAmount);

			UIComponent reflectionsEnabled = this.createMenuButton<bool>("\\reflections", this.main.Settings.EnableReflections, boolDisplay);
			reflectionsEnabled.Add(new CommandBinding<Point, int>(reflectionsEnabled.MouseScrolled, delegate(Point mouse, int scroll)
			{
				this.main.Settings.EnableReflections.Value = !this.main.Settings.EnableReflections;
			}));
			reflectionsEnabled.Add(new CommandBinding<Point>(reflectionsEnabled.MouseLeftUp, delegate(Point mouse)
			{
				this.main.Settings.EnableReflections.Value = !this.main.Settings.EnableReflections;
			}));
			settingsMenu.Children.Add(reflectionsEnabled);

			UIComponent ssaoEnabled = this.createMenuButton<bool>("\\ambient occlusion", this.main.Settings.EnableSSAO, boolDisplay);
			ssaoEnabled.Add(new CommandBinding<Point, int>(ssaoEnabled.MouseScrolled, delegate(Point mouse, int scroll)
			{
				this.main.Settings.EnableSSAO.Value = !this.main.Settings.EnableSSAO;
			}));
			ssaoEnabled.Add(new CommandBinding<Point>(ssaoEnabled.MouseLeftUp, delegate(Point mouse)
			{
				this.main.Settings.EnableSSAO.Value = !this.main.Settings.EnableSSAO;
			}));
			settingsMenu.Children.Add(ssaoEnabled);

			UIComponent bloomEnabled = this.createMenuButton<bool>("\\bloom", this.main.Renderer.EnableBloom, boolDisplay);
			bloomEnabled.Add(new CommandBinding<Point, int>(bloomEnabled.MouseScrolled, delegate(Point mouse, int scroll)
			{
				this.main.Renderer.EnableBloom.Value = !this.main.Renderer.EnableBloom;
			}));
			bloomEnabled.Add(new CommandBinding<Point>(bloomEnabled.MouseLeftUp, delegate(Point mouse)
			{
				this.main.Renderer.EnableBloom.Value = !this.main.Renderer.EnableBloom;
			}));
			settingsMenu.Children.Add(bloomEnabled);

			UIComponent dynamicShadows = this.createMenuButton<LightingManager.DynamicShadowSetting>("\\dynamic shadows", this.main.LightingManager.DynamicShadows, x => "\\" + x.ToString().ToLower());
			int numDynamicShadowSettings = typeof(LightingManager.DynamicShadowSetting).GetFields(BindingFlags.Static | BindingFlags.Public).Length;
			dynamicShadows.Add(new CommandBinding<Point, int>(dynamicShadows.MouseScrolled, delegate(Point mouse, int scroll)
			{
				int newValue = ((int)this.main.LightingManager.DynamicShadows.Value) + scroll;
				while (newValue < 0)
					newValue += numDynamicShadowSettings;
				this.main.LightingManager.DynamicShadows.Value = (LightingManager.DynamicShadowSetting)Enum.ToObject(typeof(LightingManager.DynamicShadowSetting), newValue % numDynamicShadowSettings);
			}));
			dynamicShadows.Add(new CommandBinding<Point>(dynamicShadows.MouseLeftUp, delegate(Point mouse)
			{
				this.main.LightingManager.DynamicShadows.Value = (LightingManager.DynamicShadowSetting)Enum.ToObject(typeof(LightingManager.DynamicShadowSetting), (((int)this.main.LightingManager.DynamicShadows.Value) + 1) % numDynamicShadowSettings);
			}));
			settingsMenu.Children.Add(dynamicShadows);

			// Controls menu
			Animation controlsAnimation = null;

			ListContainer controlsMenu = new ListContainer();
			controlsMenu.Visible.Value = false;
			controlsMenu.Add(new Binding<Vector2, Point>(controlsMenu.Position, x => new Vector2(0, x.Y * 0.5f), this.main.ScreenSize));
			controlsMenu.AnchorPoint.Value = new Vector2(1, 0.5f);
			this.main.UI.Root.Children.Add(controlsMenu);
			controlsMenu.Orientation.Value = ListContainer.ListOrientation.Vertical;

			Container controlsLabelPadding = this.CreateContainer();
			controlsMenu.Children.Add(controlsLabelPadding);

			ListContainer controlsLabelContainer = new ListContainer();
			controlsLabelContainer.Orientation.Value = ListContainer.ListOrientation.Vertical;
			controlsLabelPadding.Children.Add(controlsLabelContainer);

			TextElement controlsLabel = new TextElement();
			controlsLabel.FontFile.Value = "Font";
			controlsLabel.Text.Value = "\\controls title";
			controlsLabelContainer.Children.Add(controlsLabel);

			TextElement controlsScrollLabel = new TextElement();
			controlsScrollLabel.FontFile.Value = "Font";
			controlsScrollLabel.Text.Value = "\\scroll for more";
			controlsLabelContainer.Children.Add(controlsScrollLabel);

			bool controlsShown = false;

			Action hideControls = delegate()
			{
				controlsShown = false;

				this.showPauseMenu();

				if (controlsAnimation != null)
					controlsAnimation.Delete.Execute();
				controlsAnimation = new Animation
				(
					new Animation.Vector2MoveToSpeed(controlsMenu.AnchorPoint, new Vector2(1, 0.5f), Menu.hideAnimationSpeed),
					new Animation.Set<bool>(controlsMenu.Visible, false)
				);
				this.main.AddComponent(controlsAnimation);
			};

			UIComponent controlsBack = this.CreateButton("\\back", hideControls);
			controlsMenu.Children.Add(controlsBack);

			ListContainer controlsList = new ListContainer();
			controlsList.Orientation.Value = ListContainer.ListOrientation.Vertical;

			Scroller controlsScroller = new Scroller();
			controlsScroller.Add(new Binding<Vector2>(controlsScroller.Size, () => new Vector2(controlsList.Size.Value.X, this.main.ScreenSize.Value.Y * 0.5f), controlsList.Size, this.main.ScreenSize));
			controlsScroller.Children.Add(controlsList);
			controlsMenu.Children.Add(controlsScroller);

			UIComponent invertMouseX = this.createMenuButton<bool>("\\invert look x", this.main.Settings.InvertMouseX);
			invertMouseX.Add(new CommandBinding<Point, int>(invertMouseX.MouseScrolled, delegate(Point mouse, int scroll)
			{
				this.main.Settings.InvertMouseX.Value = !this.main.Settings.InvertMouseX;
			}));
			invertMouseX.Add(new CommandBinding<Point>(invertMouseX.MouseLeftUp, delegate(Point mouse)
			{
				this.main.Settings.InvertMouseX.Value = !this.main.Settings.InvertMouseX;
			}));
			controlsList.Children.Add(invertMouseX);

			UIComponent invertMouseY = this.createMenuButton<bool>("\\invert look y", this.main.Settings.InvertMouseY);
			invertMouseY.Add(new CommandBinding<Point, int>(invertMouseY.MouseScrolled, delegate(Point mouse, int scroll)
			{
				this.main.Settings.InvertMouseY.Value = !this.main.Settings.InvertMouseY;
			}));
			invertMouseY.Add(new CommandBinding<Point>(invertMouseY.MouseLeftUp, delegate(Point mouse)
			{
				this.main.Settings.InvertMouseY.Value = !this.main.Settings.InvertMouseY;
			}));
			controlsList.Children.Add(invertMouseY);

			UIComponent mouseSensitivity = this.createMenuButton<float>("\\look sensitivity", this.main.Settings.MouseSensitivity, x => ((int)Math.Round(x * 100.0f)).ToString() + "%");
			mouseSensitivity.SwallowMouseEvents.Value = true;
			mouseSensitivity.Add(new CommandBinding<Point, int>(mouseSensitivity.MouseScrolled, delegate(Point mouse, int scroll)
			{
				this.main.Settings.MouseSensitivity.Value = Math.Max(0, Math.Min(5, this.main.Settings.MouseSensitivity + (scroll * 0.1f)));
			}));
			controlsList.Children.Add(mouseSensitivity);

			Action<Property<PCInput.PCInputBinding>, string, bool, bool> addInputSetting = delegate(Property<PCInput.PCInputBinding> setting, string display, bool allowGamepad, bool allowMouse)
			{
				this.inputBindings.Add(setting);
				UIComponent button = this.createMenuButton<PCInput.PCInputBinding>(display, setting);
				button.Add(new CommandBinding<Point>(button.MouseLeftUp, delegate(Point mouse)
				{
					PCInput.PCInputBinding originalValue = setting;
					setting.Value = new PCInput.PCInputBinding();
					this.main.UI.EnableMouse.Value = false;
					input.GetNextInput(delegate(PCInput.PCInputBinding binding)
					{
						if (binding.Key == Keys.Escape)
							setting.Value = originalValue;
						else
						{
							PCInput.PCInputBinding newValue = new PCInput.PCInputBinding();
							newValue.Key = originalValue.Key;
							newValue.MouseButton = originalValue.MouseButton;
							newValue.GamePadButton = originalValue.GamePadButton;

							if (binding.Key != Keys.None)
							{
								newValue.Key = binding.Key;
								newValue.MouseButton = PCInput.MouseButton.None;
							}
							else if (allowMouse && binding.MouseButton != PCInput.MouseButton.None)
							{
								newValue.Key = Keys.None;
								newValue.MouseButton = binding.MouseButton;
							}

							if (allowGamepad)
							{
								if (binding.GamePadButton != Buttons.BigButton)
									newValue.GamePadButton = binding.GamePadButton;
							}
							else
								newValue.GamePadButton = Buttons.BigButton;

							setting.Value = newValue;
						}
						this.main.UI.EnableMouse.Value = true;
					});
				}));
				controlsList.Children.Add(button);
			};

			addInputSetting(this.main.Settings.Forward, "\\move forward", false, true);
			addInputSetting(this.main.Settings.Left, "\\move left", false, true);
			addInputSetting(this.main.Settings.Backward, "\\move backward", false, true);
			addInputSetting(this.main.Settings.Right, "\\move right", false, true);
			addInputSetting(this.main.Settings.Jump, "\\jump", true, true);
			addInputSetting(this.main.Settings.Parkour, "\\parkour", true, true);
			addInputSetting(this.main.Settings.RollKick, "\\roll / kick", true, true);
			addInputSetting(this.main.Settings.TogglePhone, "\\toggle phone", true, true);
			addInputSetting(this.main.Settings.QuickSave, "\\quicksave", true, true);

			// Mapping LMB to toggle fullscreen makes it impossible to change any other settings.
			// So don't allow it.
			addInputSetting(this.main.Settings.ToggleFullscreen, "\\toggle fullscreen", true, false);

			// Start new button
			UIComponent startNew = this.CreateButton("\\new game", delegate()
			{
				this.ShowDialog("\\alpha disclaimer", "\\play", delegate()
				{
					this.restorePausedSettings();
					this.main.CurrentSave.Value = null;
					this.main.AddComponent(new Animation
					(
						new Animation.Delay(0.2f),
						new Animation.Set<string>(this.main.MapFile, GameMain.InitialMap)
					));
				});
			});
			this.pauseMenu.Children.Add(startNew);
			startNew.Add(new Binding<bool, string>(startNew.Visible, x => x == GameMain.MenuMap, this.main.MapFile));

			// Resume button
			UIComponent resume = this.CreateButton("\\resume", delegate()
			{
				this.main.Paused.Value = false;
				this.restorePausedSettings();
			});
			resume.Visible.Value = false;
			this.pauseMenu.Children.Add(resume);
			resume.Add(new Binding<bool, string>(resume.Visible, x => x != GameMain.MenuMap, this.main.MapFile));

			// Save button
			UIComponent saveButton = this.CreateButton("\\save", delegate()
			{
				this.hidePauseMenu();

				this.saveMode.Value = true;

				this.loadSaveMenu.Visible.Value = true;
				if (this.loadSaveAnimation != null)
					this.loadSaveAnimation.Delete.Execute();
				this.loadSaveAnimation = new Animation(new Animation.Ease(new Animation.Vector2MoveToSpeed(this.loadSaveMenu.AnchorPoint, new Vector2(0, 0.5f), Menu.animationSpeed), Animation.Ease.Type.OutExponential));
				this.main.AddComponent(this.loadSaveAnimation);

				this.loadSaveShown = true;
				this.currentMenu.Value = this.loadSaveList;
			});
			saveButton.Add(new Binding<bool>(saveButton.Visible, () => this.main.MapFile != GameMain.MenuMap && (this.main.Player.Value != null && this.main.Player.Value.Active), this.main.MapFile, this.main.Player));

			this.pauseMenu.Children.Add(saveButton);

			Action showLoad = delegate()
			{
				this.hidePauseMenu();

				this.saveMode.Value = false;

				this.loadSaveMenu.Visible.Value = true;
				if (this.loadSaveAnimation != null)
					this.loadSaveAnimation.Delete.Execute();
				this.loadSaveAnimation = new Animation(new Animation.Ease(new Animation.Vector2MoveToSpeed(this.loadSaveMenu.AnchorPoint, new Vector2(0, 0.5f), Menu.animationSpeed), Animation.Ease.Type.OutExponential));
				this.main.AddComponent(this.loadSaveAnimation);

				this.loadSaveShown = true;
				this.currentMenu.Value = this.loadSaveList;
			};

			// Load button
			UIComponent load = this.CreateButton("\\load", showLoad);
			this.pauseMenu.Children.Add(load);

			// Sandbox button
			UIComponent sandbox = this.CreateButton("\\sandbox", delegate()
			{
				this.ShowDialog("\\sandbox disclaimer", "\\play anyway", delegate()
				{
					this.restorePausedSettings();
					this.main.CurrentSave.Value = null;
					this.main.AddComponent(new Animation
					(
						new Animation.Delay(0.2f),
						new Animation.Set<string>(this.main.MapFile, "sandbox")
					));
				});
			});
			this.pauseMenu.Children.Add(sandbox);
			sandbox.Add(new Binding<bool, string>(sandbox.Visible, x => x == GameMain.MenuMap, this.main.MapFile));

			// Cheat menu
#if CHEAT
			Animation cheatAnimation = null;
			bool cheatShown = false;

			ListContainer cheatMenu = new ListContainer();
			cheatMenu.Visible.Value = false;
			cheatMenu.Add(new Binding<Vector2, Point>(cheatMenu.Position, x => new Vector2(0, x.Y * 0.5f), this.main.ScreenSize));
			cheatMenu.AnchorPoint.Value = new Vector2(1, 0.5f);
			this.main.UI.Root.Children.Add(cheatMenu);
			cheatMenu.Orientation.Value = ListContainer.ListOrientation.Vertical;

			Container cheatLabelPadding = this.CreateContainer();
			cheatMenu.Children.Add(cheatLabelPadding);

			ListContainer cheatLabelContainer = new ListContainer();
			cheatLabelContainer.Orientation.Value = ListContainer.ListOrientation.Vertical;
			cheatLabelPadding.Children.Add(cheatLabelContainer);

			TextElement cheatLabel = new TextElement();
			cheatLabel.FontFile.Value = "Font";
			cheatLabel.Text.Value = "\\cheat title";
			cheatLabelContainer.Children.Add(cheatLabel);

			TextElement cheatScrollLabel = new TextElement();
			cheatScrollLabel.FontFile.Value = "Font";
			cheatScrollLabel.Text.Value = "\\scroll for more";
			cheatLabelContainer.Children.Add(cheatScrollLabel);

			Action hideCheat = delegate()
			{
				cheatShown = false;

				this.showPauseMenu();

				if (cheatAnimation != null)
					cheatAnimation.Delete.Execute();
				cheatAnimation = new Animation
				(
					new Animation.Vector2MoveToSpeed(cheatMenu.AnchorPoint, new Vector2(1, 0.5f), Menu.hideAnimationSpeed),
					new Animation.Set<bool>(cheatMenu.Visible, false)
				);
				this.main.AddComponent(cheatAnimation);
			};

			UIComponent cheatBack = this.CreateButton("\\back", hideCheat);
			cheatMenu.Children.Add(cheatBack);

			ListContainer cheatList = new ListContainer();
			cheatList.Orientation.Value = ListContainer.ListOrientation.Vertical;

			foreach (KeyValuePair<string, string> item in Menu.maps)
			{
				string m = item.Key;
				UIComponent button = this.CreateButton(item.Value, delegate()
				{
					hideCheat();
					this.restorePausedSettings();
					this.main.CurrentSave.Value = null;
					this.main.AddComponent(new Animation
					(
						new Animation.Delay(0.2f),
						new Animation.Set<string>(this.main.MapFile, m)
					));
				});
				cheatList.Children.Add(button);
			}

			Scroller cheatScroller = new Scroller();
			cheatScroller.Children.Add(cheatList);
			cheatScroller.Add(new Binding<Vector2>(cheatScroller.Size, () => new Vector2(cheatList.Size.Value.X, this.main.ScreenSize.Value.Y * 0.5f), cheatList.Size, this.main.ScreenSize));
			cheatMenu.Children.Add(cheatScroller);

			// Cheat button
			UIComponent cheat = this.CreateButton("\\cheat", delegate()
			{
				this.hidePauseMenu();

				cheatMenu.Visible.Value = true;
				if (cheatAnimation != null)
					cheatAnimation.Delete.Execute();
				cheatAnimation = new Animation(new Animation.Ease(new Animation.Vector2MoveToSpeed(cheatMenu.AnchorPoint, new Vector2(0, 0.5f), Menu.animationSpeed), Animation.Ease.Type.OutExponential));
				this.main.AddComponent(cheatAnimation);

				cheatShown = true;
				this.currentMenu.Value = cheatList;
			});
			cheat.Add(new Binding<bool, string>(cheat.Visible, x => x == GameMain.MenuMap, this.main.MapFile));
			this.pauseMenu.Children.Add(cheat);
#endif

			// Controls button
			UIComponent controlsButton = this.CreateButton("\\controls", delegate()
			{
				this.hidePauseMenu();

				controlsMenu.Visible.Value = true;
				if (controlsAnimation != null)
					controlsAnimation.Delete.Execute();
				controlsAnimation = new Animation(new Animation.Ease(new Animation.Vector2MoveToSpeed(controlsMenu.AnchorPoint, new Vector2(0, 0.5f), Menu.animationSpeed), Animation.Ease.Type.OutExponential));
				this.main.AddComponent(controlsAnimation);

				controlsShown = true;
				this.currentMenu.Value = controlsList;
			});
			this.pauseMenu.Children.Add(controlsButton);

			// Settings button
			UIComponent settingsButton = this.CreateButton("\\options", delegate()
			{
				this.hidePauseMenu();

				settingsMenu.Visible.Value = true;
				if (settingsAnimation != null)
					settingsAnimation.Delete.Execute();
				settingsAnimation = new Animation(new Animation.Ease(new Animation.Vector2MoveToSpeed(settingsMenu.AnchorPoint, new Vector2(0, 0.5f), Menu.animationSpeed), Animation.Ease.Type.OutExponential));
				this.main.AddComponent(settingsAnimation);

				settingsShown = true;

				this.currentMenu.Value = settingsMenu;
			});
			this.pauseMenu.Children.Add(settingsButton);

#if DEVELOPMENT
			// Edit mode toggle button
			UIComponent switchToEditMode = this.CreateButton("\\edit mode", delegate()
			{
				this.pauseMenu.Visible.Value = false;
				this.main.EditorEnabled.Value = true;
				this.main.Paused.Value = false;
				if (this.pauseAnimation != null)
				{
					this.pauseAnimation.Delete.Execute();
					this.pauseAnimation = null;
				}
				IO.MapLoader.Load(this.main, null, this.main.MapFile, true);
				this.main.CurrentSave.Value = null;
			});
			this.pauseMenu.Children.Add(switchToEditMode);
#endif

			// Credits window
			Animation creditsAnimation = null;
			bool creditsShown = false;

			ListContainer creditsMenu = new ListContainer();
			creditsMenu.Visible.Value = false;
			creditsMenu.Add(new Binding<Vector2, Point>(creditsMenu.Position, x => new Vector2(0, x.Y * 0.5f), this.main.ScreenSize));
			creditsMenu.AnchorPoint.Value = new Vector2(1, 0.5f);
			this.main.UI.Root.Children.Add(creditsMenu);
			creditsMenu.Orientation.Value = ListContainer.ListOrientation.Vertical;

			Container creditsLabelPadding = this.CreateContainer();
			creditsMenu.Children.Add(creditsLabelPadding);

			ListContainer creditsLabelContainer = new ListContainer();
			creditsLabelContainer.Orientation.Value = ListContainer.ListOrientation.Vertical;
			creditsLabelPadding.Children.Add(creditsLabelContainer);

			TextElement creditsLabel = new TextElement();
			creditsLabel.FontFile.Value = "Font";
			creditsLabel.Text.Value = "\\credits title";
			creditsLabelContainer.Children.Add(creditsLabel);

			TextElement creditsScrollLabel = new TextElement();
			creditsScrollLabel.FontFile.Value = "Font";
			creditsScrollLabel.Text.Value = "\\scroll for more";
			creditsLabelContainer.Children.Add(creditsScrollLabel);

			Action hideCredits = delegate()
			{
				creditsShown = false;

				this.showPauseMenu();

				if (creditsAnimation != null)
					creditsAnimation.Delete.Execute();
				creditsAnimation = new Animation
				(
					new Animation.Vector2MoveToSpeed(creditsMenu.AnchorPoint, new Vector2(1, 0.5f), Menu.hideAnimationSpeed),
					new Animation.Set<bool>(creditsMenu.Visible, false)
				);
				this.main.AddComponent(creditsAnimation);
			};

			UIComponent creditsBack = this.CreateButton("\\back", delegate()
			{
				hideCredits();
			});
			creditsMenu.Children.Add(creditsBack);

			TextElement creditsDisplay = new TextElement();
			creditsDisplay.FontFile.Value = "Font";
			creditsDisplay.Text.Value = this.Credits = File.ReadAllText("attribution.txt");

			Scroller creditsScroller = new Scroller();
			creditsScroller.Add(new Binding<Vector2>(creditsScroller.Size, () => new Vector2(creditsDisplay.Size.Value.X, this.main.ScreenSize.Value.Y * 0.5f), creditsDisplay.Size, this.main.ScreenSize));
			creditsScroller.Children.Add(creditsDisplay);
			creditsMenu.Children.Add(creditsScroller);

			// Credits button
			UIComponent credits = this.CreateButton("\\credits", delegate()
			{
				this.hidePauseMenu();

				creditsMenu.Visible.Value = true;
				if (creditsAnimation != null)
					creditsAnimation.Delete.Execute();
				creditsAnimation = new Animation(new Animation.Ease(new Animation.Vector2MoveToSpeed(creditsMenu.AnchorPoint, new Vector2(0, 0.5f), Menu.animationSpeed), Animation.Ease.Type.OutExponential));
				this.main.AddComponent(creditsAnimation);

				creditsShown = true;
				this.currentMenu.Value = creditsDisplay;
			});
			credits.Add(new Binding<bool, string>(credits.Visible, x => x == GameMain.MenuMap, this.main.MapFile));
			this.pauseMenu.Children.Add(credits);

			// Main menu button
			UIComponent mainMenu = this.CreateButton("\\main menu", delegate()
			{
				this.ShowDialog
				(
					"\\quit prompt", "\\quit",
					delegate()
					{
						this.main.CurrentSave.Value = null;
						this.main.MapFile.Value = GameMain.MenuMap;
						this.main.Paused.Value = false;
					}
				);
			});
			this.pauseMenu.Children.Add(mainMenu);
			mainMenu.Add(new Binding<bool, string>(mainMenu.Visible, x => x != GameMain.MenuMap, this.main.MapFile));

			// Exit button
			UIComponent exit = this.CreateButton("\\exit", delegate()
			{
				if (this.main.MapFile.Value != GameMain.MenuMap)
				{
					this.ShowDialog
					(
						"\\exit prompt", "\\exit",
						delegate()
						{
							throw new GameMain.ExitException();
						}
					);
				}
				else
					throw new GameMain.ExitException();
			});
			this.pauseMenu.Children.Add(exit);

			bool saving = false;
			this.input.Bind(this.main.Settings.QuickSave, PCInput.InputState.Down, delegate()
			{
				if (!saving && !this.main.Paused && this.main.MapFile != GameMain.MenuMap && this.main.Player.Value != null && this.main.Player.Value.Active)
				{
					saving = true;
					Container notification = new Container();
					notification.Tint.Value = Microsoft.Xna.Framework.Color.Black;
					notification.Opacity.Value = 0.5f;
					TextElement notificationText = new TextElement();
					notificationText.Name.Value = "Text";
					notificationText.FontFile.Value = "Font";
					notificationText.Text.Value = "\\saving";
					notification.Children.Add(notificationText);
					this.main.UI.Root.GetChildByName("Notifications").Children.Add(notification);
					this.main.AddComponent(new Animation
					(
						new Animation.Delay(0.01f),
						new Animation.Execute(delegate()
						{
							this.main.SaveOverwrite();
						}),
						new Animation.Set<string>(notificationText.Text, "\\saved"),
						new Animation.Parallel
						(
							new Animation.FloatMoveTo(notification.Opacity, 0.0f, 1.0f),
							new Animation.FloatMoveTo(notificationText.Opacity, 0.0f, 1.0f)
						),
						new Animation.Execute(notification.Delete),
						new Animation.Execute(delegate()
						{
							saving = false;
						})
					));
				}
			});

			// Escape key
			// Make sure we can only pause when there is a player currently spawned
			// Otherwise we could save the current map without the player. And that would be awkward.
			Func<bool> canPause = delegate()
			{
				if (this.main.EditorEnabled)
					return false;

				if (this.main.MapFile.Value == GameMain.MenuMap)
					return !this.main.Paused; // Only allow pausing, don't allow unpausing

				return true;
			};

			Action togglePause = delegate()
			{
				if (this.dialog != null)
				{
					this.dialog.Delete.Execute();
					this.dialog = null;
					return;
				}
				else if (settingsShown)
				{
					hideSettings();
					return;
				}
				else if (controlsShown)
				{
					hideControls();
					return;
				}
				else if (creditsShown)
				{
					hideCredits();
					return;
				}
				else if (this.loadSaveShown)
				{
					this.hideLoadSave();
					return;
				}
#if CHEAT
				else if (cheatShown)
				{
					hideCheat();
					return;
				}
#endif

				if (this.main.MapFile.Value == GameMain.MenuMap)
				{
					if (this.currentMenu.Value == null)
						this.savePausedSettings();
				}
				else
				{
					this.main.Paused.Value = !this.main.Paused;

					if (this.main.Paused)
						this.savePausedSettings();
					else
						this.restorePausedSettings();
				}
			};

			this.input.Add(new CommandBinding(input.GetKeyDown(Keys.Escape), () => canPause() || this.dialog != null, togglePause));
			this.input.Add(new CommandBinding(input.GetButtonDown(Buttons.Start), canPause, togglePause));
			this.input.Add(new CommandBinding(input.GetButtonDown(Buttons.B), () => canPause() || this.dialog != null, togglePause));

#if !DEVELOPMENT
				// Pause on window lost focus
				this.Deactivated += delegate(object sender, EventArgs e)
				{
					if (!this.main.Paused && this.main.MapFile.Value != GameMain.MenuMap && !this.EditorEnabled)
					{
						this.main.Paused.Value = true;
						this.savePausedSettings();
					}
				};
#endif
			// Gamepad menu code

			int selected = 0;

			Func<UIComponent, int, int, int> nextMenuItem = delegate(UIComponent menu, int current, int delta)
			{
				int end = menu.Children.Count;
				if (current <= 0 && delta < 0)
					return end - 1;
				else if (current >= end - 1 && delta > 0)
					return 0;
				else
					return current + delta;
			};

			Func<UIComponent, bool> isButton = delegate(UIComponent item)
			{
				return item.Visible && item.GetType() == typeof(Container) && (item.MouseLeftUp.HasBindings || item.MouseScrolled.HasBindings);
			};

			Func<UIComponent, bool> isScrollButton = delegate(UIComponent item)
			{
				return item.Visible && item.GetType() == typeof(Container) && item.MouseScrolled.HasBindings;
			};

			this.input.Add(new NotifyBinding(delegate()
			{
				UIComponent menu = this.currentMenu;
				if (menu != null && menu != creditsDisplay && this.main.GamePadConnected)
				{
					foreach (UIComponent item in menu.Children)
						item.Highlighted.Value = false;

					int i = 0;
					foreach (UIComponent item in menu.Children)
					{
						if (isButton(item))
						{
							item.Highlighted.Value = true;
							selected = i;
							break;
						}
						i++;
					}
				}
			}, this.currentMenu));

			Action<int> moveSelection = delegate(int delta)
			{
				UIComponent menu = this.currentMenu;
				if (menu != null && this.dialog == null)
				{
					if (menu == this.loadSaveList)
						delta = -delta;
					else if (menu == creditsDisplay)
					{
						Scroller scroll = (Scroller)menu.Parent;
						scroll.MouseScrolled.Execute(new Point(), delta * -4);
						return;
					}

					Container button = (Container)menu.Children[selected];
					button.Highlighted.Value = false;

					int i = nextMenuItem(menu, selected, delta);
					while (true)
					{
						UIComponent item = menu.Children[i];
						if (isButton(item))
						{
							selected = i;
							break;
						}

						i = nextMenuItem(menu, i, delta);
					}

					button = (Container)menu.Children[selected];
					button.Highlighted.Value = true;

					if (menu.Parent.Value.GetType() == typeof(Scroller))
					{
						Scroller scroll = (Scroller)menu.Parent;
						scroll.ScrollTo(button);
					}
				}
			};

			Func<bool> enableGamepad = delegate()
			{
				return this.main.Paused || this.main.MapFile.Value == GameMain.MenuMap;
			};

			this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.LeftThumbstickUp), enableGamepad, delegate()
			{
				moveSelection(-1);
			}));

			this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.DPadUp), enableGamepad, delegate()
			{
				moveSelection(-1);
			}));

			this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.LeftThumbstickDown), enableGamepad, delegate()
			{
				moveSelection(1);
			}));

			this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.DPadDown), enableGamepad, delegate()
			{
				moveSelection(1);
			}));

			this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.A), enableGamepad, delegate()
			{
				if (this.dialog != null)
					this.dialog.GetChildByName("Okay").MouseLeftUp.Execute(new Point());
				else
				{
					UIComponent menu = this.currentMenu;
					if (menu != null && menu != creditsDisplay )
					{
						UIComponent selectedItem = menu.Children[selected];
						if (isButton(selectedItem) && selectedItem.Highlighted)
							selectedItem.MouseLeftUp.Execute(new Point());
					}
				}
			}));

			Action<int> scrollButton = delegate(int delta)
			{
				UIComponent menu = this.currentMenu;
				if (menu != null && menu != creditsDisplay && this.dialog == null)
				{
					UIComponent selectedItem = menu.Children[selected];
					if (isScrollButton(selectedItem) && selectedItem.Highlighted)
						selectedItem.MouseScrolled.Execute(new Point(), delta);
				}
			};

			this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.LeftThumbstickLeft), enableGamepad, delegate()
			{
				scrollButton(-1);
			}));

			this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.DPadLeft), enableGamepad, delegate()
			{
				scrollButton(-1);
			}));

			this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.LeftThumbstickRight), enableGamepad, delegate()
			{
				scrollButton(1);
			}));

			this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.DPadRight), enableGamepad, delegate()
			{
				scrollButton(1);
			}));
		}

		public void Update(float dt)
		{
			if (this.main.GamePadState.Value.IsConnected != this.main.LastGamePadState.Value.IsConnected)
			{
				// Re-bind inputs so their string representations are properly displayed
				// We need to show both PC and gamepad bindings

				foreach (Property<PCInput.PCInputBinding> binding in this.inputBindings)
					binding.Reset();
			}
		}
	}
}