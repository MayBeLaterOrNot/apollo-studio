﻿using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GradientStop = Avalonia.Media.GradientStop;
using IBrush = Avalonia.Media.IBrush;
using LinearGradientBrush = Avalonia.Media.LinearGradientBrush;
using Avalonia.Threading;

using Apollo.Components;
using Apollo.Core;
using Apollo.Devices;
using Apollo.Elements;
using Apollo.Enums;
using Apollo.Structures;

namespace Apollo.DeviceViewers {
    public class FadeViewer: UserControl {
        public static readonly string DeviceIdentifier = "fade";

        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
            
            canvas = this.Get<Canvas>("Canvas");
            PickerContainer = this.Get<Grid>("PickerContainer");
            Picker = this.Get<ColorPicker>("Picker");
            Gradient = this.Get<LinearGradientBrush>("Gradient");

            PlaybackMode = this.Get<ComboBox>("PlaybackMode");
            Duration = this.Get<Dial>("Duration");
            Gate = this.Get<Dial>("Gate");

            PositionText = this.Get<TextBlock>("PositionText");
            Display = this.Get<TextBlock>("Display");
            Input = this.Get<TextBox>("Input");
        }
        
        Fade _fade;
        
        Dial Duration, Gate;
        ComboBox PlaybackMode;
        TextBlock PositionText, Display;
        TextBox Input;
        Canvas canvas;
        Grid PickerContainer;
        ColorPicker Picker;
        LinearGradientBrush Gradient;

        List<FadeThumb> thumbs = new List<FadeThumb>();

        int? current;

        public void Contents_Insert(int index, Color color) {
            FadeThumb thumb = new FadeThumb();
            thumbs.Insert(index, thumb);
            Canvas.SetLeft(thumb, _fade.GetPosition(index) * 200);

            thumb.Moved += Thumb_Move;
            thumb.Focused += Thumb_Focus;
            thumb.Deleted += Thumb_Delete;
            thumb.Fill = color.ToBrush();
            
            canvas.Children.Add(thumb);
            if (current != null && index <= current) current++;
        }

        public void Contents_Remove(int index) {
            if (current != null) {
                if (index < current) current--;
                else if (index == current) Expand(null);
            }

            canvas.Children.Remove(thumbs[index]);
            thumbs.RemoveAt(index);
        }

        public FadeViewer(Fade fade) {
            InitializeComponent();

            _fade = fade;
            _fade.Generated += Gradient_Generate;

            PlaybackMode.SelectedIndex = (int)_fade.PlayMode;

            Duration.UsingSteps = _fade.Time.Mode;
            Duration.Length = _fade.Time.Length;
            Duration.RawValue = _fade.Time.Free;

            Gate.RawValue = _fade.Gate * 100;
            
            thumbs.Add(this.Get<FadeThumb>("ThumbStart"));
            thumbs[0].Fill = _fade.GetColor(0).ToBrush();

            for (int i = 1; i < _fade.Count - 1; i++) 
                Contents_Insert(i, _fade.GetColor(i));

            thumbs.Add(this.Get<FadeThumb>("ThumbEnd"));
            thumbs.Last().Fill = _fade.GetColor(_fade.Count - 1).ToBrush();

            Input.GetObservable(TextBox.TextProperty).Subscribe(Input_Changed);

            Gradient_Generate();
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            _fade.Generated -= Gradient_Generate;
            _fade = null;
        }

        public void Expand(int? index) {
            if (current != null) {
                PickerContainer.ColumnDefinitions[0].Width = new GridLength(0);
                thumbs[current.Value].Unselect();

                PositionText.Text = "";
                Display.Text = "";

                if (index == current) {
                    current = null;
                    return;
                }
            }

            if (index != null) {
                PickerContainer.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                thumbs[index.Value].Select();

                if (index != 0 && index != _fade.Count - 1) {
                    PositionText.Text = "Position:";
                    Display.Text = $"{(Math.Round(_fade.GetPosition(index.Value) * 1000) / 10).ToString()}%";
                }

                Picker.SetColor(_fade.GetColor(index.Value));
            }
            
            current = index;
        }

        void Canvas_MouseDown(object sender, PointerPressedEventArgs e) {
            if (e.MouseButton == MouseButton.Left && e.ClickCount == 2) {
                int index;
                double x = e.Device.GetPosition(canvas).X - 7;

                for (index = 0; index < thumbs.Count; index++)
                    if (x < Canvas.GetLeft(thumbs[index])) break;
                
                double pos = x / 200;

                List<int> path = Track.GetPath(_fade);

                Program.Project.Undo.Add($"Fade Color {index + 1} Inserted", () => {
                    ((Fade)Track.TraversePath(path)).Remove(index);
                }, () => {
                    ((Fade)Track.TraversePath(path)).Insert(index, new Color(), pos);
                });

                _fade.Insert(index, new Color(), pos);
            }
        }

        void Thumb_Delete(FadeThumb sender) {
            int index = thumbs.IndexOf(sender);

            Color uc = _fade.GetColor(index).Clone();
            double up = _fade.GetPosition(index);
            List<int> path = Track.GetPath(_fade);

            Program.Project.Undo.Add($"Fade Color {index + 1} Removed", () => {
                ((Fade)Track.TraversePath(path)).Insert(index, uc, up);
            }, () => {
                ((Fade)Track.TraversePath(path)).Remove(index);
            });

            _fade.Remove(index);
        }

        void Thumb_Move(FadeThumb sender, double change, double? total) {
            int i = thumbs.IndexOf(sender);

            double left = Canvas.GetLeft(thumbs[i - 1]) + 1;
            double right = Canvas.GetLeft(thumbs[i + 1]) - 1;

            double old = Canvas.GetLeft(sender);
            double x = old + change;

            x = (x < left)? left : x;
            x = (x > right)? right : x;

            double pos = x / 200;

            if (total != null) {
                double u = x - total.Value;
                List<int> path = Track.GetPath(_fade);

                Program.Project.Undo.Add($"Fade Color {i + 1} Moved", () => {
                    ((Fade)Track.TraversePath(path)).SetPosition(i, u / 200);
                }, () => {
                    ((Fade)Track.TraversePath(path)).SetPosition(i, pos);
                });
            }

            _fade.SetPosition(i, pos);
        }

        public void SetPosition(int index, double position) {
            Canvas.SetLeft(thumbs[index], position * 200);

            if (index == current)
                Display.Text = $"{(Math.Round(_fade.GetPosition(index) * 1000) / 10).ToString()}%";
        }

        void Thumb_Focus(FadeThumb sender) => Expand(thumbs.IndexOf(sender));

        void Color_Changed(Color color, Color old) {
            if (current != null) {
                if (old != null) {
                    Color u = old.Clone();
                    Color r = color.Clone();
                    int index = current.Value;
                    List<int> path = Track.GetPath(_fade);

                    Program.Project.Undo.Add($"Fade Color {current + 1} Changed to {r.ToHex()}", () => {
                        ((Fade)Track.TraversePath(path)).SetColor(index, u.Clone());
                    }, () => {
                        ((Fade)Track.TraversePath(path)).SetColor(index, r.Clone());
                    });
                }

                _fade.SetColor(current.Value, color);
            }
        }

        public void SetColor(int index, Color color) {
            if (current == index)
                Picker.SetColor(color);

            thumbs[index].Fill = color.ToBrush();
        }

        void Gradient_Generate() {
            if (Program.Project.IsDisposing) return;
            
            Dispatcher.UIThread.InvokeAsync(() => {
                Gradient.GradientStops.Clear();

                if (_fade != null)
                    for (int i = 0; i < _fade.Count; i++)
                        Gradient.GradientStops.Add(new GradientStop(_fade.GetColor(i).ToAvaloniaColor(), _fade.GetPosition(i)));
            });
        }

        void Duration_Changed(double value, double? old) {
            if (old != null && old != value) {
                int u = (int)old.Value;
                int r = (int)value;
                List<int> path = Track.GetPath(_fade);

                Program.Project.Undo.Add($"Fade Duration Changed to {r}{Duration.Unit}", () => {
                    ((Fade)Track.TraversePath(path)).Time.Free = u;
                }, () => {
                    ((Fade)Track.TraversePath(path)).Time.Free = r;
                });
            }

            _fade.Time.Free = (int)value;
        }

        public void SetDurationValue(int duration) => Duration.RawValue = duration;

        void Duration_ModeChanged(bool value, bool? old) {
            if (old != null && old != value) {
                bool u = old.Value;
                bool r = value;
                List<int> path = Track.GetPath(_fade);

                Program.Project.Undo.Add($"Fade Duration Switched to {(r? "Steps" : "Free")}", () => {
                    ((Fade)Track.TraversePath(path)).Time.Mode = u;
                }, () => {
                    ((Fade)Track.TraversePath(path)).Time.Mode = r;
                });
            }

            _fade.Time.Mode = value;
        }

        public void SetMode(bool mode) => Duration.UsingSteps = mode;

        void Duration_StepChanged(int value, int? old) {
            if (old != null && old != value) {
                int u = old.Value;
                int r = value;
                List<int> path = Track.GetPath(_fade);

                Program.Project.Undo.Add($"Fade Duration Changed to {Length.Steps[r]}", () => {
                    ((Fade)Track.TraversePath(path)).Time.Length.Step = u;
                }, () => {
                    ((Fade)Track.TraversePath(path)).Time.Length.Step = r;
                });
            }
        }

        public void SetDurationStep(Length duration) => Duration.Length = duration;

        void Gate_Changed(double value, double? old) {
            if (old != null && old != value) {
                double u = old.Value / 100;
                double r = value / 100;
                List<int> path = Track.GetPath(_fade);

                Program.Project.Undo.Add($"Fade Gate Changed to {value}{Gate.Unit}", () => {
                    ((Fade)Track.TraversePath(path)).Gate = u;
                }, () => {
                    ((Fade)Track.TraversePath(path)).Gate = r;
                });
            }

            _fade.Gate = value / 100;
        }

        public void SetGate(double gate) => Gate.RawValue = gate * 100;

        void PlaybackMode_Changed(object sender, SelectionChangedEventArgs e) {
            FadePlaybackType selected = (FadePlaybackType)PlaybackMode.SelectedIndex;

            if (_fade.PlayMode != selected) {
                FadePlaybackType u = _fade.PlayMode;
                FadePlaybackType r = selected;
                List<int> path = Track.GetPath(_fade);

                Program.Project.Undo.Add($"Fade Playback Mode Changed to {r}", () => {
                    ((Fade)Track.TraversePath(path)).PlayMode = u;
                }, () => {
                    ((Fade)Track.TraversePath(path)).PlayMode = r;
                });

                _fade.PlayMode = selected;
            }
        }

        public void SetPlaybackMode(FadePlaybackType mode) => PlaybackMode.SelectedIndex = (int)mode;

        Action Input_Update;

        void Input_Changed(string text) {
            if (text == null) return;
            if (text == "") return;

            Input_Update = () => { Input.Text = (Math.Round(_fade.GetPosition(current.Value) * 1000) / 10).ToString(); };

            if (double.TryParse(text, out double value)) {
                double min = _fade.GetPosition(current.Value - 1) * 100 + 0.5;
                double max = _fade.GetPosition(current.Value + 1) * 100 - 0.5;

                if (min <= value && value <= max) {
                    _fade.SetPosition(current.Value, value / 100);
                    Input_Update = () => { Input.Foreground = (IBrush)Application.Current.Styles.FindResource("ThemeForegroundBrush"); };
                } else {
                    Input_Update = () => { Input.Foreground = (IBrush)Application.Current.Styles.FindResource("ErrorBrush"); };
                }

                Input_Update += () => {
                    if (value < 0) text = $"-{text.Substring(1).TrimStart('0')}";
                    else if (value > 0) text = text.TrimStart('0');
                    else text = "0";

                    if (value <= 0) text = "0";

                    int upper = (int)Math.Pow(10, ((int)max).ToString().Length) - 1;
                    if (value > upper) text = upper.ToString();
                    
                    Input.Text = text;
                };
            }

            Dispatcher.UIThread.InvokeAsync(() => {
                Input_Update?.Invoke();
                Input_Update = null;
            });
        }

        double oldValue;

        void DisplayPressed(object sender, PointerPressedEventArgs e) {
            if (e.MouseButton == MouseButton.Left && e.ClickCount == 2) {
                oldValue = Math.Round(_fade.GetPosition(current.Value) * 1000) / 10;
                Input.Text = oldValue.ToString();

                Input.SelectionStart = 0;
                Input.SelectionEnd = Input.Text.Length;
                Input.CaretIndex = Input.Text.Length;

                Input.Opacity = 1;
                Input.IsHitTestVisible = true;
                Input.Focus();

                e.Handled = true;
            }
        }
        
        void Input_LostFocus(object sender, RoutedEventArgs e) {
            double raw = _fade.GetPosition(current.Value);

            Input.Text = (Math.Round(raw * 1000) / 10).ToString();

            Input.Opacity = 0;
            Input.IsHitTestVisible = false;

            double u = oldValue / 100;
            double r = raw;
            int i = current.Value;
            List<int> path = Track.GetPath(_fade);

            Program.Project.Undo.Add($"Fade Color {i + 1} Moved", () => {
                ((Fade)Track.TraversePath(path)).SetPosition(i, u);
            }, () => {
                ((Fade)Track.TraversePath(path)).SetPosition(i, r);
            });
        }

        void Input_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Return)
                this.Focus();

            e.Key = Key.None;
        }

        void Input_KeyUp(object sender, KeyEventArgs e) => e.Key = Key.None;

        void Input_MouseUp(object sender, PointerReleasedEventArgs e) => e.Handled = true;
    }
}
