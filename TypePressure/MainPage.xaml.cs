using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TypePressure.Rendering;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace TypePressure;

public sealed partial class MainPage : Page
{
    private bool _ready, _syncing, _dialogOpen, _wasPaused, _wasFullscreen;
    private CancellationTokenSource? _toast;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _demoExitTimer;
    private bool _exitHovered;
    private int _portraitScene = -1;
    private int _portraitCycle = -1;
    public MainPage()
    {
        InitializeComponent();
        _demoExitTimer=DispatcherQueue.CreateTimer();
        _demoExitTimer.Interval=TimeSpan.FromSeconds(3);
        _demoExitTimer.Tick+=(_,_)=>{_demoExitTimer.Stop();if(!_exitHovered)DemoExitButton.Visibility=Visibility.Collapsed;};
        _ready=true;
        Chamber.Updated+=UpdateStatus; Chamber.DoubleTapped+=(_,_)=>{if(Chamber.Simulation.Demo)SetDemo(false);};
        Loaded+=(_,_)=>{Focus(FocusState.Programmatic);SyncControls();if(App.PortraitDemo){SetDemo(true);Chamber.Simulation.Paused=true;}};
        Chamber.PointerPressed+=(_,_)=>{if(FocusManager.GetFocusedElement(XamlRoot!) is not TextBox)Focus(FocusState.Programmatic);};
        try { Chamber.Simulation.Paused=!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled; } catch { }
    }
    private void PresetClick(object sender,RoutedEventArgs e)
    {
        Chamber.Simulation.SetMode(Enum.Parse<Experiment>((string)((ToggleButton)sender).Tag));SyncControls();Chamber.Refresh();
    }
    private void SyncControls()
    {
        if(!_ready)return;_syncing=true;var state=Chamber.Simulation;
        WeightSlider.Maximum=state.Anybody?900:1000;WidthSlider.Minimum=state.Anybody?50:25;WidthSlider.Maximum=state.Anybody?150:151;
        state.Weight=Math.Clamp(state.Weight,100,(float)WeightSlider.Maximum);state.Width=Math.Clamp(state.Width,(float)WidthSlider.Minimum,(float)WidthSlider.Maximum);if(state.Anybody)state.Slant=0;
        WeightSlider.Value=state.Weight;WidthSlider.Value=state.Width;SlantSlider.Value=state.Slant;SlantSlider.IsEnabled=!state.Anybody;SlantControls.Opacity=state.Anybody?.4:1;LeanForceItem.IsEnabled=!state.Anybody;
        FontNote.Text=state.Anybody?"2 axes. A whole lot of character.":"13 axes. Endless possibilities.";
        WeightValue.Text=state.Weight.ToString("0");WidthValue.Text=state.Width.ToString("0");SlantValue.Text=state.Slant.ToString("0.#")+"°";
        foreach(var button in new[]{BreathingButton,TugButton,ShockButton,StormButton})
        {
            var selected=Enum.Parse<Experiment>((string)button.Tag)==state.Mode;button.IsChecked=selected;
        }
        _syncing=false;UpdateStatus();
    }
    private void AxisChanged(object sender,RangeBaseValueChangedEventArgs e)
    {
        if(!_ready||_syncing)return;
        var state=Chamber.Simulation;state.Weight=(float)WeightSlider.Value;state.Width=(float)WidthSlider.Value;state.Slant=(float)SlantSlider.Value;
        WeightValue.Text=state.Weight.ToString("0");WidthValue.Text=state.Width.ToString("0");SlantValue.Text=state.Slant.ToString("0.#")+"°";Chamber.Refresh();
    }
    private void FontChanged(object sender,SelectionChangedEventArgs e)
    {
        if(!_ready)return;var state=Chamber.Simulation;state.Anybody=FontSelect.SelectedIndex==1;
        if(state.Anybody)state.Fields.RemoveAll(f=>f.Kind==ForceKind.Lean);SyncControls();Chamber.Refresh();
    }
    private void MaterialChanged(object sender,TextChangedEventArgs e){if(!_ready)return;Chamber.Simulation.Text=MaterialInput.Text;Chamber.Refresh();}
    private void AddForceClick(object sender,RoutedEventArgs e)
    {
        if(Chamber.Simulation.AddForce(Enum.Parse<ForceKind>((string)((MenuFlyoutItem)sender).Tag)) is null)ShowToast("Six forces is plenty of pressure. Remove one to add another.");
        Chamber.Refresh();Focus(FocusState.Programmatic);
    }
    private void RemoveForceClick(object sender,RoutedEventArgs e){Chamber.Simulation.RemoveSelected();Chamber.Refresh();}
    private void FreezeClick(object sender,RoutedEventArgs e)=>ToggleFreeze();
    private void ToggleFreeze(){Chamber.Simulation.Paused=!Chamber.Simulation.Paused;Chamber.Simulation.PointerDown=false;Chamber.Refresh();UpdateStatus();}
    private void ResetClick(object sender,RoutedEventArgs e){Chamber.Simulation.Reset();SyncControls();Chamber.Refresh();ShowToast("Fresh air. A clean chamber.");}
    private void UpdateStatus()
    {
        if(!_ready)return;var state=Chamber.Simulation;
        if(App.PortraitDemo && state.Demo)
        {
            var cycle=(int)(state.Time/PressureSimulation.DemoDuration);
            if(!state.Paused && (_portraitScene!=state.DemoScene || _portraitCycle!=cycle))
            {
                _portraitScene=state.DemoScene;_portraitCycle=cycle;
                Console.WriteLine($"PORTRAIT_SCENE {state.DemoScene} CYCLE {cycle} TIME {state.Time:F3} UTC {DateTimeOffset.UtcNow:O}");
            }
            return;
        }
        FreezeButton.Content=state.Paused?"▷   Resume     ␣":"Ⅱ   Freeze      ␣";
        LiveLabel.Text=state.Paused?"TIME IS FROZEN":"LIVE SIMULATION";
        LiveDot.Fill=(Brush)Application.Current.Resources[state.Paused?"Accent":"Live"];
        FieldCount.Text=$"{state.Fields.Count} FORCE FIELD{(state.Fields.Count==1?"":"S")}";
    }
    private void DemoClick(object sender,RoutedEventArgs e)=>SetDemo(!Chamber.Simulation.Demo);
    private void ExitDemoClick(object sender,RoutedEventArgs e)=>SetDemo(false);
    private void RevealDemoExit()
    {
        if(!Chamber.Simulation.Demo || App.PortraitDemo)return;
        DemoExitButton.Visibility=Visibility.Visible;
        _demoExitTimer.Stop();_demoExitTimer.Start();
    }
    private void DemoPointerMoved(object sender,PointerRoutedEventArgs e)=>RevealDemoExit();
    private void DemoExitPointerEntered(object sender,PointerRoutedEventArgs e){_exitHovered=true;_demoExitTimer.Stop();}
    private void DemoExitPointerExited(object sender,PointerRoutedEventArgs e){_exitHovered=false;RevealDemoExit();}
    private void EscapeAccelerator(KeyboardAccelerator sender,KeyboardAcceleratorInvokedEventArgs e)
    {
        if(Chamber.Simulation.Demo){SetDemo(false);e.Handled=true;}
    }
    private void SetDemo(bool enabled)
    {
        if(Chamber.Simulation.Demo==enabled)return;
        if(enabled){_wasPaused=Chamber.Simulation.Paused;_wasFullscreen=App.MainWindow!.AppWindow.Presenter.Kind==AppWindowPresenterKind.FullScreen;Chamber.Simulation.Paused=false;if(!App.PortraitDemo)App.MainWindow.AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);}
        else{Chamber.Simulation.Paused=_wasPaused;if(!_wasFullscreen && !App.PortraitDemo)App.MainWindow!.AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);}
        var visibility=enabled?Visibility.Collapsed:Visibility.Visible;
        Header.Visibility=ControlDeck.Visibility=StatusBar.Visibility=ExperimentsBar.Visibility=visibility;
        Root.RowDefinitions[0].Height=new GridLength(enabled?0:98);
        Root.RowDefinitions[2].Height=new GridLength(enabled?0:136);
        Root.RowDefinitions[3].Height=new GridLength(enabled?0:40);
        ToastHost.Visibility=Visibility.Collapsed;Chamber.BeginDemo(enabled);App.MainWindow!.Activate();DispatcherQueue.TryEnqueue(()=>Focus(FocusState.Programmatic));
        if(enabled && !App.PortraitDemo){RevealDemoExit();ShowToast("Double-click anywhere to exit · Esc also returns",4000);}
        else{_demoExitTimer.Stop();DemoExitButton.Visibility=Visibility.Collapsed;_exitHovered=false;}
    }
    private void FullscreenClick(object sender,RoutedEventArgs e)=>ToggleFullscreen();
    private static void ToggleFullscreen(){var window=App.MainWindow!.AppWindow;window.SetPresenter(window.Presenter.Kind==AppWindowPresenterKind.FullScreen?AppWindowPresenterKind.Overlapped:AppWindowPresenterKind.FullScreen);}
    private void OnKeyDown(object sender,KeyRoutedEventArgs e)
    {
        if(!_ready||_dialogOpen)return;
        if(Chamber.Simulation.Demo)
        {
            if(e.Key is VirtualKey.Escape or VirtualKey.D){SetDemo(false);e.Handled=true;}
            else if(App.PortraitDemo && e.Key==VirtualKey.Space)
            {
                Chamber.BeginDemo(true);Chamber.Simulation.Paused=false;_portraitScene=-1;_portraitCycle=-1;
                Console.WriteLine($"PORTRAIT_STARTED UTC {DateTimeOffset.UtcNow:O}");e.Handled=true;
            }
            return;
        }
        var focused=FocusManager.GetFocusedElement(XamlRoot!) as DependencyObject;
        for(var current=focused;current is not null;current=VisualTreeHelper.GetParent(current))if(current is TextBox||current is ComboBox)return;
        if(e.Key==VirtualKey.D){SetDemo(true);e.Handled=true;}
        else if(e.Key==VirtualKey.F11){ToggleFullscreen();e.Handled=true;}
        else if(e.Key==VirtualKey.Escape&&App.MainWindow!.AppWindow.Presenter.Kind==AppWindowPresenterKind.FullScreen){ToggleFullscreen();e.Handled=true;}
        else if(e.Key==VirtualKey.Space&&focused is not ButtonBase){ToggleFreeze();e.Handled=true;}
        else if(e.Key==VirtualKey.R){ResetClick(this,new RoutedEventArgs());e.Handled=true;}
        else if(focused is not Slider&&Chamber.HandleKey(e.Key))e.Handled=true;
    }
    private async void SaveClick(object sender,RoutedEventArgs e)
    {
        var svg=(string)((MenuFlyoutItem)sender).Tag=="svg";var wasPaused=Chamber.Simulation.Paused;
        Chamber.Simulation.Paused=true;_dialogOpen=true;
        try
        {
            var bytes=Chamber.Export(svg);
            var picker=new FileSavePicker{SuggestedStartLocation=PickerLocationId.PicturesLibrary,SuggestedFileName="Type Pressure"};
            picker.FileTypeChoices.Add(svg?"SVG vector specimen":"PNG specimen",new List<string>{svg?".svg":".png"});
            WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!));
            var file=await picker.PickSaveFileAsync();
            if(file is not null){await FileIO.WriteBytesAsync(file,bytes);ShowToast("Specimen saved. A little pressure, preserved.");}
        }
        catch(Exception ex){ShowToast("Could not save the specimen. "+ex.Message);}
        finally{Chamber.Simulation.Paused=wasPaused;_dialogOpen=false;UpdateStatus();}
    }
    private async void HelpClick(object sender,RoutedEventArgs e)
    {
        var wasPaused=Chamber.Simulation.Paused;Chamber.Simulation.Paused=true;_dialogOpen=true;
        try
        {
            var dialog=new ContentDialog{XamlRoot=XamlRoot,Title="Type has a wild side.",CloseButtonText="Let’s put type under pressure",Content="Hold + drag to inflate letters and pull a current.\n\nPull the side handles to squeeze the chamber.\n\nAdd a force, then drag its orange marker. F cycles through fields; arrow keys move the selected field; Delete removes it.\n\nSpace freezes time. R resets the chamber. F11 toggles fullscreen.\n\nD opens Demo mode: seven looping, text-only sequences, including Specimen Storm. Double-click or Escape brings the controls back.\n\nRoboto Flex and Anybody are bundled under the SIL Open Font License. Anybody supports weight and width; its slant control rests."};
            await dialog.ShowAsync();
        }
        finally{Chamber.Simulation.Paused=wasPaused;_dialogOpen=false;}
    }
    private async void ShowToast(string message,int duration=2800)
    {
        _toast?.Cancel();_toast=new CancellationTokenSource();ToastText.Text=message;ToastHost.Visibility=Visibility.Visible;
        try{await Task.Delay(duration,_toast.Token);ToastHost.Visibility=Visibility.Collapsed;}catch(OperationCanceledException){}
    }
}
