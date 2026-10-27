# Converting nacBackupWPF from WPF to Avalonia, using the nac.Forms NuGet package

This document describes how to migrate the `nacBackupWPF` application — a WPF/XAML
desktop program (an XML backup utility) — to a cross-platform desktop UI built with
the **`nac.Forms`** NuGet package, which constructs **Avalonia** windows in C# code.

The migration is already started in `nacBackupWPF/Window1_Avalonia.cs`, and the
Avalonia window is launched from `nacBackupWPF/App.xaml.cs`. This document
explains the conversion concept by concept and gives a per-region mapping of the
existing WPF UI onto the `nac.Forms` fluent builder API.

---

## 1. The two technologies involved

There are two separate layers, and it is worth keeping them distinct:

* **Avalonia** — a cross-platform (Windows / Linux / macOS) .NET UI framework, the
  community alternative to WPF. It supplies the underlying primitives
  (`Window`, `TextBox`, `DataGrid`, `Menu`, …) and its own data-binding engine.
* **nac.Forms** — a NuGet package *by the same author as this project*
  (`Nathaniel Collier`, package id `nac.Forms`, version **12.1.1.3** in this
  repo). It is a thin fluent builder on top of Avalonia that lets you assemble a
  whole form in C# instead of writing XAML. One `nac.Forms.Form` wraps one
  Avalonia `Window` plus an internal `Grid` host, and you add rows to it by
  chaining builder methods such as `form.Text(...)`, `form.Button(...)`, and
  `form.HorizontalGroup(...)`.

So a `Form` *is* an Avalonia window; you just build it in code. `nac.Forms`
depends on Avalonia 12.1.1, `Avalonia.Controls.DataGrid` 12.1.2,
`Avalonia.Themes.Simple` 12.1.1, `nac.CSSParsing`, `nac.Logging`, and
`nac.utilities` (see the package `.nuspec`).

---

## 2. How the Avalonia window is launched (App.xaml.cs → Window1_Avalonia)

This is the most important part of the migration, because WPF and Avalonia start
their UI loops very differently.

### 2.1 The WPF way (before)

In WPF the process was:

1. `App` is a `System.Windows.Application` declared in `App.xaml`, with
   `Startup="App_OnStartup"`.
2. `App_OnStartup` created the main window and called `ShowDialog()`:

   ```csharp
   private void runWPF()
   {
       var mainWin = new Window1();
       mainWin.ShowDialog();
   }
   ```

3. `Window1` (from `Window1.xaml`) ran the WPF message pump, and when it closed,
   `App.Current.Shutdown()` ended the process.

### 2.2 The Avalonia/nac.Forms way (now)

There is no XAML window to instantiate. Instead `Window1_Avalonia` builds a
`nac.Forms.Form` in code and asks it to display itself. `Form.Display(...)` both
**shows** the window **and** **runs the Avalonia message loop** (it ultimately
calls `app.Run()` on the Avalonia `Application` that `Form.NewForm` created
internally), so it does not return until the window is closed.

`Window1_Avalonia.cs` (the class that replaces `Window1`):

```csharp
public class Window1_Avalonia
{
    private nac.Forms.Form mainForm;

    private MainViewModel myModel =>
        mainForm?.DataContext as MainViewModel;

    public void Run()
    {
        // 1) Create the form. The delegate is a ConfigureAppBuilder, i.e.
        //    Action<Avalonia.AppBuilder>, that is invoked once to configure the
        //    Avalonia application that the form is built on. Here it forces a
        //    light theme.
        mainForm = nac.Forms.Form.NewForm(config =>
        {
            config.AfterSetup(_ =>
            {
                Avalonia.Application.Current.RequestedThemeVariant =
                    Avalonia.Styling.ThemeVariant.Light;
            });
        });

        // 2) Hand the form the same view model the WPF window used. Setting the
        //    DataContext is what makes every "bind by property name" call below
        //    (TextFor("Foo"), TextBoxFor("Foo"), ...) resolve against the
        //    MainViewModel. See section 3.
        mainForm.DataContext = new MainViewModel();

        // 3) Build the whole UI by chaining builder methods onto the form.
        buildForm(mainForm);

        // 4) Show the window AND run the Avalonia app loop.
        mainForm.Display(onDisplay: onFormDisplay);
    }

    private void buildForm(nac.Forms.Form f)
    {
        // The form's title (replaces the WPF Window Title attribute).
        f.Title = "Nathaniel Collier Backup Utility";

        // ...the form builder calls go here (see section 5)...
    }

    private async Task onFormDisplay(Form _arg)
    {
        // runs once, on the UI thread, right after the window is shown
    }
}
```

`App.xaml.cs` then launches this instead of `runWPF()`:

```csharp
public partial class App : Application
{
    private async void App_OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            runAvalonia();           // was: runWPF();
            // runWPF();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Exception details: {ex}",
                "Error on app setup");
        }
        finally
        {
            App.Current.Shutdown();   // ends the WPF app after the Avalonia loop exits
        }
    }

    private void runAvalonia()
    {
        var avMainWin = new Window1_Avalonia();
        avMainWin.Run();
    }

    private void runWPF()
    {
        var mainWin = new Window1();
        mainWin.ShowDialog();
    }
}
```

### 2.3 About the App.xaml / App.xaml.cs hybrid

For this migration the project deliberately keeps a WPF `Application` (`App`
inherits `System.Windows.Application`) because:

* `App.xaml` still declares `Startup="App_OnStartup"` and the WPF build pipeline
  (`<UseWPF>true</UseWPF>`) compiles it. `App_OnStartup` is the **one** place
  that decides which UI to run.
* `Form.NewForm()` builds and runs the **Avalonia** application on its own
  (internally `AppBuilder.Configure<AClass>().SetupWithoutStarting()` … `.Run()`).
  It does **not** depend on the WPF `Application` object.
* `App.Current.Shutdown()` ends the WPF `Application` so the process exits once the
  Avalonia window has closed.

`App.xaml` is empty of resources, so it can be removed later if the build stops
needing a WPF entry point.

---

## 3. How data binding works in nac.Forms (the single most important concept)

In WPF you wrote XAML bindings like `Text="{Binding BackupSetPath}"`.
In `nac.Forms` you write the **property name as a string** and the builder creates
the binding for you:

```csharp
f.TextFor("BackupSetPath");
f.TextBoxFor("BackupSetPath");
```

The string is a **dot-delimited path** resolved by reflection against the form's
`DataContext`. So these all work:

```csharp
f.TextFor("BackupSetPath");                 // DataContext.BackupSetPath
f.TextFor("SelectedBackup.Name");            // DataContext.SelectedBackup.Name
f.TextFor("SelectedBackup.BackupLocations");  // a sub-collection
```

This works for two reasons:

1. **The view model is an `INotifyPropertyChanged` object.**
   Every view model in this project inherits from
   `nac.ViewModelBase.ViewModelBase` (the `nac.ViewModelBase` NuGet package), which
   implements `INotifyPropertyChanged` and provides the
   `GetValue(() => this.Foo)` / `SetValue(() => this.Foo, value)` /
   `OnPropertyChanged("Foo")` helpers used throughout. Because the view model
   already raises `PropertyChanged`, when a property changes the bound control
   updates automatically.

2. **nac.Forms subscribes to those change notifications.**
   When a control binds to `SelectedBackup.Name`, `nac.Forms` walks the parent
   chain on `DataContext` (`SelectedBackup` → `Name`) and re-resolves whenever any
   ancestor raises `PropertyChanged` for the next link, so a change in the parent
   propagates to the child binding.

### 3.1 Two binding sources

`form.DataContext = new MainViewModel()` sets a **typed binding source**. There is
also a dynamic source, `form.Model` (a `nac.utilities.BindableDynamicDictionary`):

* **Typed (this app):** `form.DataContext = viewModel; f.TextFor("BackupSetPath");`
  binds to `viewModel.BackupSetPath`. This is what we use.
* **Dynamic:** `form.Model["backupSetPath"] = "…"; f.TextFor("backupSetPath");`
  binds to the dictionary key.

`nac.Forms.Form.DataContext` is itself stored as a special key
(`SpecialModelKeys.DataContext == "datacontext"`) inside the dynamic model, and
`getBindingSource()` prefers it when present — which is why simply assigning
`form.DataContext` switches the whole form over to type-based binding.

### 3.2 Property-name resolution

The field-path parsing is done by
`nac.Forms.model.ModelFieldNamePathInfo`: the string is split on `.`, the first
token is the current property, the rest is the relative child path. So
`"SelectedBackup.Name"` → current `SelectedBackup`, child `Name`.

### 3.3 One- vs two-way

* `Text(string literal, style)` — a constant label.
* `TextFor(string modelFieldName, string defaultValue = null, style)` — a one-way
  display.
* `TextBoxFor(string modelFieldName, …)` — **two-way** edit.
* `Progress(string modelFieldName, int min, int max, style)` — one-way meter.

One-way display (`TextFor`, `Progress`) is enough for read-mostly fields;
two-way (`TextBoxFor`) is needed for fields the user edits.

---

## 4. Converting the view models / models

The view models are `nac.ViewModelBase.ViewModelBase` subclasses. The
`GetValue`/`SetValue`/`OnPropertyChanged` calls in them are **not** WPF — they
come from the framework-agnostic `nac.ViewModelBase` package, so those classes
stay unchanged:

| Class | WPF? | Action |
|-------|------|--------|
| `BackupType` | No | unchanged. Holds `TimeStamPFormat`, `Name`, `DestinationPath`, an `ObservableCollection<BackupLocationType> BackupLocations`, and XML load/save. |
| `BackupLocationType` | No | unchanged. Fields `BackupLocationPath`, `BackupLocationName`, `BackupLocationSize`. |
| `BackupDirectoryEntry` | No | unchanged. Fields `Path`, `Notes`, `FileSize`, `FileSizeBytes`, `BackupName`. |
| `BackupDirectoryViewModel` | No | unchanged. Aggregates `BackupEntries`, `EntriesToDisplay`, size totals, `SelectedBackupName`. |
| `MainViewModel` | **Uses `System.Windows`** | needs conversion (below) before the form can be built. |

### 4.1 What to change in `MainViewModel` to make it WPF-free

`MainViewModel` currently references WPF. To bind it as an Avalonia form's
`DataContext`, two things must go:

1. **`System.Windows.Media.Brush` / `Brushes.Blue` / `Brushes.Green`** in the
   `BackupProgressBrush` property. `ProgressBar` styling in `nac.Forms` is driven
   by `Style` (`nac.Forms.model.Style`), whose colors are
   `Avalonia.Media.Color`, not WPF `Brush`. `BackupProgressBrush` is no longer
   needed; drop it (and the `OnPropertyChanged("BackupProgressBrush")` call).

2. **`System.Windows.Threading.DispatcherTimer`** (`timer`). Avalonia has no WPF
   dispatcher timer. Replace it with a plain `System.Threading.Timer` and push
   updates onto the UI thread with `form.InvokeAsync(...)` (or bind the elapsed
   time via a model property the UI reads).

3. **`System.Windows.Threading` / `System.Windows.Media` usings** can be removed
   once 1 and 2 are done.

> Note: the `GetValue`/`SetValue`/`OnPropertyChanged` calls, the
> `ObservableCollection<>` fields, and the whole backup-engine logic in
> `MainViewModel` are unchanged — only the UI-framework bits are removed. The
> backup *engine* (the `Ionic.Zip.ZipFile` work, progress callbacks, file I/O)
> lives in the view model and does not depend on any UI framework.

### 4.2 Why the form must reference these models

`nac.Forms` is reflection-based, so the form does not compile against the view
model at build time; it resolves property names against `DataContext` at runtime.
That means a typo in a property string (`TextFor("BackupSetPat h")`) is a
**runtime** error, not a compile error. (One such typo, `TimeStamPFormat`, is
present in `BackupType` and does not break the build because it is a normal C#
property, but `TextFor`/`TextBoxFor` strings are not type-checked — be careful.)

---

## 5. Converting each region of `Window1.xaml`

The WPF UI in `Window1.xaml` is reproduced in `Window1_Avalonia.buildForm(Form f)`.
Below is a region-by-region mapping. (These are the target builder calls; they are
illustrative, not yet written into `Window1_Avalonia.cs`.)

| WPF control (XAML) | nac.Forms builder call |
|---|---|
| `Window Title="…"` | `f.Title = "…";` |
| `TextBlock Text="{Binding X}"` (one-way) | `f.TextFor("X")` or `f.Text("literal")` |
| `TextBox Text="{Binding X}"` (two-way) | `f.TextBoxFor("X")` |
| `ProgressBar Value="{Binding X}"` | `f.Progress("X", 0, 100, style: …)` |
| `Menu` + `MenuItem`s | `f.Menu(new[] { new nac.Forms.model.MenuItem { … } })` |
| `ComboBox` with `ItemTemplate` + `SelectedValue` + `ItemsSource` | `f.DropDown<T>("itemsField","selectedField", onSelectionChanged, itemTemplate)` |
| `StackPanel Orientation="Horizontal"` | `f.HorizontalGroup(g => …)` (grid) or `f.HorizontalStack(g => …)` (stack) |
| `StackPanel Orientation="Vertical"` | `f.VerticalGroup(g => …)` or `f.VerticalStack(g => …)` |
| `DockPanel` | `f.VerticalDock(g => …)` |
| `ListView` + `GridView` | `f.Table<T>("itemsField", columns: …)` (DataGrid-based) |
| `ListView` (list) | `f.List<T>("itemsField", itemTemplate)` |
| `DataTemplate` | an `Action<Form>` passed as the `itemTemplate`/`populate` delegate |
| `NacWPFControls:DirectoryPicker` | `f.DirectoryPathFor("field", onDirectoryPathChanged)` |
| File open/save dialog | `await f.ShowOpenFolderDialog()` / `f.FilePathFor("field", …)` |
| `Expander` | no direct equivalent; use `f.Tabs(…)` or a `VerticalGroup` you expand/collapse |
| `RichTextBox` + `FlowDocument` | no rich-text control; use `f.TextBoxFor("Log", …, multiline: true)` and build plain text |
| `System.Windows.MessageBox` | **no built-in message box** in nac.Forms 12.1.1.3; replace confirmations with a `DisplayChildForm` dialog, or `Avalonia.Controls.ApplicationDialogs` / a simple `MessageBox`-style child form (see 5.5) |
| Per-row `DirectoryPicker` in a `GridView` cell | a `Column.template` delegate that puts a `DirectoryPathFor` into the cell (see 5.2) |

### 5.1 Concretely, the top of the form

The first horizontal row in the existing `Window1_Avalonia.cs` is:

```csharp
f.HorizontalGroup(h =>
{
    h.Text("nacBackup - ")
     .Text(lib.settings.appVersion, style: "color:blue;");
});
```

Note the `style:` argument. `nac.Forms.model.Style` has an **implicit conversion
from a CSS string** (via `nac.CSSParsing`), so `style: "color:blue;"` is parsed
into a `Style` object. You can also build one explicitly:

```csharp
new nac.Forms.model.Style
{
    height = 30,
    width = 700,
    isVisibleModelName = "SomeBoolFlag",   // control shows only when this model bool is true
}
```

### 5.2 Converting the backup-locations table (the `ListView`/`GridView` row)

The WPF `ListView` with a `GridView` maps to a `Table<T>`. The path column in WPF
hosted a `DirectoryPicker` inside each row; in `nac.Forms` that is a column with a
**`template`** delegate:

```csharp
f.Table<BackupLocationType>(
    "BackupLocations",
    columns: new[]
    {
        new nac.Forms.model.Column
        {
            Header = "Path",
            template = form =>
            {
                // 'form' here is bound to the current row's BackupLocationType,
                // so this binds to that row's BackupLocationPath.
                form.DirectoryPathFor("BackupLocationPath");
            },
        },
        new nac.Forms.model.Column { Header = "Name",   modelBindingPropertyName = "Name" },
        new nac.Forms.model.Column { Header = "Size",   modelBindingPropertyName = "Size" },
    });
```

`nac.Forms.model.Column` has `Header`, `modelBindingPropertyName` (a text column),
and `template` (a per-row `Action<Form>` for custom controls like a directory
picker). `nac.Forms.Table<T>` is built on `Avalonia.Controls.DataGrid`, and
nac.Forms loads the DataGrid theme the first time it is used.

### 5.3 The backup-set dropdown with a custom item template

The WPF `ComboBox` used `BackupTypeComboBoxDataTemplate` (Name + Destination
Directory). In nac.Forms that is `DropDown<T>` with an `itemTemplate` delegate:

```csharp
f.DropDown<BackupType>(
    "itemsField",
    "selectedField",
    onSelectionChanged: selected => { /* react to the chosen backup set */ },
    itemTemplate: form =>
        form.HorizontalGroup(g =>
            g.TextFor("Name")
             .TextFor("DestinationPath")),
    style: "width:500px;");
```

### 5.4 Buttons and the backup engine

WPF `Button … Click="…" → handler` → `f.Button("label", async () => { … })`.
The backup engine itself (`Ionic.Zip`, the `SaveLogToZip`/`SaveNotesToZip`
threads, progress reporting) is UI-framework independent and moves into the
callback bodies, marshaling UI updates with `await form.InvokeAsync(...)`.

### 5.5 Replacing message boxes

`nac.Forms.Form` 12.1.1.3 has **no built-in `MessageBox`**. It does provide
`DisplayChildForm(...)`, `FilePathFor(...)`, and `ShowOpenFolderDialog(...)`.
For confirmation prompts (the WPF code uses
`System.Windows.MessageBox.Show(..., OKCancel, …)`), implement a small
`DisplayChildForm` that builds an "OK/Cancel" form and returns the result, or
reference `Avalonia.Controls.ApplicationDialogs`. This is a deliberate
simplification of the WPF message boxes.

---

## 6. Converting `BackupDirectoryBrowser`

`BackupDirectoryBrowser.xaml`/`.xaml.cs` opens a directory, lists the zip backup
files it finds, shows per-file notes/size, and lets the user open a backup. It
maps the same way:

* `Window` → a child form opened by the main form's `f.DisplayChildForm(...)`.
* `NacWPFControls:DirectoryPicker` → `f.DirectoryPathFor("DirectoryPath")`.
* The `ListView` of `BackupDirectoryEntry` → `f.Table<BackupDirectoryEntry>(…)`
  or `f.List<BackupDirectoryEntry>(…)`.
* The "Open" button per row → a `Column.template` delegate
  (`form.Button("Open", async () => await OpenBackup(entry))`).
* `BackupDirectoryViewModel` is **already WPF-free** and can be used unchanged as
  the child form's `DataContext`.

`BackupDirectoryViewModel` aggregates `BackupEntries` (added via a worker thread
during a zip scan), computes `EntriesToDisplay`/`SelectedBackupTypeTotalSize`/
`TotalBackupsSize` from the selected `BackupName`, and is the model for the
browser. Its `RefreshBackupEntryes()` / `SelectedBackupTypeChanged()` fire
`OnPropertyChanged` so the bound UI refreshes — the same mechanism the main form
relies on, so no change is needed.

---

## 7. Project / package changes (`nacBackupWPF.csproj`)

Current `<ItemGroup>`:

```xml
<PackageReference Include="DotNetZip" Version="1.16.0" />
<PackageReference Include="nac.Forms" Version="12.1.1.3" />
<PackageReference Include="nac.ViewModelBase" Version="1.0.1" />
<PackageReference Include="nac.wpf.Controls" Version="1.0.1" />
<PackageReference Include="nac.wpf.controls.BusyControl" Version="2.0.0" />
<PackageReference Include="nac.wpf.Extensions" Version="1.0.2" />
```

* `nac.Forms 12.1.1.3` and `nac.ViewModelBase 1.0.1` stay.
* `nac.wpf.Controls`, `nac.wpf.controls.BusyControl`, and `nac.wpf.Extensions`
  are **WPF-only** and are not used by the Avalonia form. They can be removed once
  nothing in the project (e.g. `Window1.xaml`, `BackupDirectoryBrowser.xaml`,
  `BusyIndicator`) references them. The Avalonia conversion in
  `Window1_Avalonia.cs` does not use any of them.
* `nac.Forms` transitively brings in Avalonia 12.1.1, `Avalonia.Controls.DataGrid`
  12.1.2, `Avalonia.Themes.Simple` 12.1.1, `nac.CSSParsing`, `nac.Logging`, and
  `nac.utilities`, so no separate Avalonia package reference is needed.

While `App.xaml` / `Window1.xaml` / `BackupDirectoryBrowser.xaml` remain,
`<UseWPF>true</UseWPF>` and `<TargetFramework>net10.0-windows</TargetFramework>`
must stay so the WPF build still works. As the XAML windows are removed and the
app is driven purely through `Window1_Avalonia.Run()`, the project can be made
pure-Avalonia.

---

## 8. Converting the backup-engine threads

The backup works by compressing a directory (the "backup set") into a zip archive
on a background thread:

```
mainForm.DataContext = new MainViewModel();        // 3.1
model.StartBackupTimer();
// build an Ionic.Zip.ZipFile, add the selected backup locations, Save() on a
// new Thread; report progress via zip_SaveProgress, which writes to the log and
// increments FileCount / DirectoryCount / CompressedBytesCounter /
// UnCompressedBytesCounter in the view model.
```

In WPF the progress callback updated a `RichTextBox.Document` and a
`ProgressBar.Foreground` (`BackupProgressBrush`) and a `BusyIndicator` via
`Dispatcher.Invoke`. In `nac.Forms`:

* The log `RichTextBox`/`FlowDocument` becomes a plain multi-line
  `f.TextBoxFor("Log", …, multiline: true)`. There is no rich-text control.
* `BackupProgressBrush` (a WPF `Brush`) is removed; if a meter is wanted,
  `f.Progress("BackupProgressPercent", 0, 100, …)` already reflects progress.
* UI updates from the zip thread go through `await model.Form.InvokeAsync(...)`
  (the `nac.Forms` `InvokeAsync` method), instead of `Dispatcher.Invoke`.

> The `zip_SaveProgress` / `Clear()` / `StartBackupTimer()` /
> `StopBackupTimer()` logic in `MainViewModel` and the backup threads are
> **UI-framework independent** — they call `model.SetProperty(...)`, which fires
> `OnPropertyChanged` and therefore updates the bound controls automatically.
> Only the *control references* (not the model) have to be removed.

---

## 9. Per-file conversion checklist

| File | Status | Notes |
|------|--------|-------|
| `App.xaml` | keep | WPF `Application` entry point; `Startup="App_OnStartup"` decides which UI to launch. |
| `App.xaml.cs` | keep / minor | `runAvalonia()` is active, `runWPF()` is commented out. No code change needed to start the Avalonia window. |
| `Window1_Avalonia.cs` | **in progress** | Starts the conversion: creates the `Form`, sets a `MainViewModel` `DataContext`, builds one row, and calls `Display(...)`. **Finish `buildForm` to cover every section of `Window1.xaml`, and replace `MessageBox`/`DispatcherTimer`/`Brush` usages (section 5.5, 8).** |
| `Window1.xaml` (`Window1.xaml.cs`) | keep until Avalonia window is complete | The WPF main window. Delete when `Window1_Avalonia` is the live UI. |
| `BackupDirectoryBrowser.xaml(.cs)` | to convert | Open as a child form (section 6). |
| `MainViewModel.cs` | to convert | Remove `System.Windows.Media.Brush` and `System.Windows.Threading.DispatcherTimer`. |
| `BackupType.cs`, `BackupLocationType.cs`, `BackupDirectoryEntry.cs`, `BackupDirectoryViewModel.cs` | keep | Already WPF-free. |
| `nacBackupWPF.csproj` | to trim | Remove the WPF-only package refs after the XAML windows are gone (section 7). |

### Conversion checklist

- [x] `App.xaml.cs` launches `Window1_Avalonia.Run()` (Avalonia) instead of `runWPF()`.
- [x] `Window1_Avalonia.cs` creates a `nac.Forms.Form`, sets a `MainViewModel` `DataContext`, and calls `Display(...)`.
- [ ] Finish `buildForm` to cover every region of `Window1.xaml` (section 5): menu, backup-set dropdown, backup-locations `Table`, progress meter, notes, log, and the Backup/Stop buttons, wiring each to the existing `*_Click` logic moved into `ButtonClick` callbacks.
- [ ] Make `MainViewModel` WPF-free (remove `Brush` / `DispatcherTimer`, section 4.1).
- [ ] Convert `BackupDirectoryBrowser` to a child form (section 6).
- [ ] Replace WPF `MessageBox` confirmations with a child-form dialog (section 5.5).
- [ ] Trim the WPF-only package references from `nacBackupWPF.csproj` (section 7).
- [ ] Build the solution and fix any `nac.Forms` property-name typos (these are runtime, not compile, errors — see 4.2).
- [ ] Run end-to-end: load a backup set, run a backup, and confirm the UI updates.

---

## Appendix A — `nac.Forms` API used by this project

`Form` (from `nac.Forms.Form`, partial across several source files):

* `static Form NewForm(ConfigureAppBuilder beforeAppBuilderInit = null)` — `ConfigureAppBuilder` is `Action<Avalonia.AppBuilder>`.
* `void Display(int height = 600, int width = 800, Func<Form, Task<bool?>> onClosing = null, Func<Form, Task> onDisplay = null)` — show + run the app loop.
* `Task DisplayChildForm(Action<Form> setupChildForm, int height, int width, Func<Form, Task<bool?>> onClosing = null, Func<Form, Task> onDisplay = null, bool useIsolatedModelForThisChildForm = false, bool isDialog = false)`
* `object DataContext { get; set; }` — the typed binding source.
* `BindableDynamicDictionary Model { get; set; }` — the dynamic binding source.
* `string Title { get; set; }`
* Row builders (each returns `Form`): `Text`, `TextFor`, `TextBoxFor`, `Button`,
  `Progress`, `DateFor`, `Image`, `List<T>`, `Table<T>`, `DropDown<T>`,
  `Autocomplete<T>`, `SimpleDropDown<T>`, `Menu`, `Panel<T>`, `Tabs`,
  `DirectoryPathFor`, `FilePathFor`, `SpinningElipse`, `LoadingTextAnimation`,
  `DebugAvalonia`.
* `Task<T> _Internal_ShowDialog(Window subWindow)`, `Task ShowOpenFolderDialog()`.

`nac.Forms.model`:

* `Style` — `{ width, height, isVisibleModelName, isHiddenModelName, TooltipText, … }` with `implicit operator from string` (CSS).
* `Column` — `{ Header, modelBindingPropertyName, template (Action<Form>) }` for `Table<T>` / `TableView<T>` columns.
* `MenuItem` — `{ Header, Items, Action }` for `Menu`.
* `TabCreationInfo` — `{ Header, Populate (Action<Form>) }` for `Tabs`.
* `SpecialModelKeys.DataContext == "datacontext"` — the model key used to store the typed `DataContext`.

`nac.Forms.controls` (the Avalonia `UserControl`s nac.Forms wraps):
`DirectoryPicker`, `FilePicker`, `ElipseSpinnerControl`, `LoadingIndicatorText`.

> The full, authoritative signature list for `nac.Forms.Form` can be obtained from
> the dll at `~/.nuget/packages/nac.forms/12.1.1.3/lib/net10.0/nac.Forms.dll`
> (e.g. with a reflection dump). The builder surface was verified against this
> version.
