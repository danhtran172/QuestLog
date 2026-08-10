using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GongSolutions.Wpf.DragDrop;
using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Data;
using MasterPlay.Infrastructure.Data.Services;
using MasterPlay.UI.Shared.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.CompilerServices;

namespace MasterPlay.UI.Features.Settings;

public partial class CatalogItem : ObservableObject
{
    public int Id { get; init; }
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string? icon;
    public string? FullPath { get; init; }
}

public partial class CatalogSettingsViewModel : BaseViewModel, IDropTarget
{
    public ObservableCollection<string> Categories { get; } = ["Genre", "Theme", "Progress", "Ownership"];
    [ObservableProperty] private string selectedCategory = "Genre";
    [ObservableProperty] private ObservableCollection<CatalogItem> items = [];
    public bool CanAdd => SelectedCategory is "Genre" or "Theme";
    public event Action? BackRequested;

    public CatalogSettingsViewModel()
    {
        RuntimeHelpers.RunClassConstructor(typeof(DataService).TypeHandle);
        Load();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(CanAdd));
        Load();
    }

    [RelayCommand]
    private void Add() => Items.Add(new CatalogItem { Name = string.Empty });

    [RelayCommand]
    private void Delete(CatalogItem? item)
    {
        if (item != null) Items.Remove(item);
    }

    [RelayCommand]
    private void PasteIcon(CatalogItem? item)
    {
        if (item == null || !Clipboard.ContainsImage())
        {
            StatusMessage = "Copy a PNG image first.";
            return;
        }
        var image = Clipboard.GetImage();
        if (image == null) return;
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        item.Icon = Convert.ToBase64String(stream.ToArray());
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            if (Items.Any(i => string.IsNullOrWhiteSpace(i.Name))) throw new InvalidOperationException("Name cannot be empty.");
            if (Items.GroupBy(i => i.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) throw new InvalidOperationException("Names must be unique.");
            using var db = new MasterPlayContext();
            switch (SelectedCategory)
            {
                case "Genre": SaveGenres(db); break;
                case "Theme": SaveThemes(db); break;
                case "Progress": SaveProgresses(db); break;
                case "Ownership": SaveOwnerships(db); break;
            }
            db.SaveChanges();
            StatusMessage = "Saved.";
            Load();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand] private void Back() => BackRequested?.Invoke();

    private void Load()
    {
        using var db = new MasterPlayContext();
        Items = SelectedCategory switch
        {
            "Genre" => new ObservableCollection<CatalogItem>(db.Genres.OrderBy(x => x.SortOrder).Select(x => new CatalogItem { Id = x.Id, Name = x.Name, Icon = x.IconData })),
            "Theme" => new ObservableCollection<CatalogItem>(db.Themes.OrderBy(x => x.SortOrder).Select(x => new CatalogItem { Id = x.Id, Name = x.Name, Icon = x.IconData })),
            "Progress" => new ObservableCollection<CatalogItem>(db.ProgressStatuses.OrderBy(x => x.SortOrder).Select(x => new CatalogItem { Id = x.Id, Name = x.Name, Icon = x.IconData })),
            _ => new ObservableCollection<CatalogItem>(db.OwnershipStatuses.OrderBy(x => x.SortOrder).Select(x => new CatalogItem { Id = x.Id, Name = x.Name, Icon = x.IconData }))
        };
    }

    private void SaveGenres(MasterPlayContext db) => SaveList(Items, db.Genres, (x, i) => { x.Name = i.Name.Trim(); x.IconData = i.Icon; x.SortOrder = Items.IndexOf(i); }, i => new Genre { Name = i.Name.Trim(), IconData = i.Icon, SortOrder = Items.IndexOf(i) });
    private void SaveThemes(MasterPlayContext db) => SaveList(Items, db.Themes, (x, i) => { x.Name = i.Name.Trim(); x.IconData = i.Icon; x.SortOrder = Items.IndexOf(i); }, i => new Theme { Name = i.Name.Trim(), IconData = i.Icon, SortOrder = Items.IndexOf(i) });
    private void SaveProgresses(MasterPlayContext db) => SaveList(Items, db.ProgressStatuses, (x, i) => { x.Name = i.Name.Trim(); x.IconData = i.Icon; x.SortOrder = Items.IndexOf(i); }, _ => throw new InvalidOperationException("Progress cannot be added."));
    private void SaveOwnerships(MasterPlayContext db) => SaveList(Items, db.OwnershipStatuses, (x, i) => { x.Name = i.Name.Trim(); x.IconData = i.Icon; x.SortOrder = Items.IndexOf(i); }, _ => throw new InvalidOperationException("Ownership cannot be added."));

    private static void SaveList<T>(ObservableCollection<CatalogItem> source, DbSet<T> records, Action<T, CatalogItem> update, Func<CatalogItem, T> create) where T : class
    {
        var existing = records.ToList();
        var retained = source.Where(i => i.Id != 0).Select(i => i.Id).ToHashSet();
        foreach (var old in existing.Where(x => !retained.Contains((int)x.GetType().GetProperty("Id")!.GetValue(x)!)).ToList()) records.Remove(old);
        foreach (var item in source)
        {
            var entity = item.Id == 0 ? create(item) : existing.Single(x => (int)x.GetType().GetProperty("Id")!.GetValue(x)! == item.Id);
            update(entity, item);
            if (item.Id == 0) records.Add(entity);
        }
    }

    void IDropTarget.DragOver(IDropInfo dropInfo) => dropInfo.Effects = DragDropEffects.Move;
    void IDropTarget.Drop(IDropInfo dropInfo)
    {
        if (dropInfo.Data is CatalogItem item && dropInfo.InsertIndex >= 0)
        {
            var oldIndex = Items.IndexOf(item);
            var newIndex = Math.Min(dropInfo.InsertIndex, Items.Count - 1);
            if (oldIndex != newIndex) Items.Move(oldIndex, newIndex);
        }
    }
}
