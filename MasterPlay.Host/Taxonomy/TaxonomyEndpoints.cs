using MasterPlay.Core.Entities;
using MasterPlay.Core.Taxonomy;
using MasterPlay.Host.Auth;
using MasterPlay.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MasterPlay.Host.Taxonomy;

/// <summary>
/// Taxonomy theo chu so huu + nhom theme. Spec 2026-10-04-theme-groups-design §5.
///
/// Mot bo logic cho hai "chu the" (Actor):
///   - mode thu vien: Actor = 'user:&lt;id&gt;' / 'guest:local' — chi ghi du lieu CA NHAN;
///   - Admin Console: Actor = 'system:shared' — chi ghi du lieu CHUNG, duoc dat icon.
/// Hang "thay duoc" = chung + cua chinh Actor; hang "sua duoc" = cua chinh Actor.
/// Voi admin hai tap trung nhau: chi du lieu chung.
/// </summary>
public static class TaxonomyEndpoints
{
    private sealed record Actor(string OwnerKey)
    {
        public bool IsAdmin => TaxonomyRules.IsShared(OwnerKey);
        public bool CanSee(string ownerKey) => TaxonomyRules.IsShared(ownerKey) || ownerKey == OwnerKey;
        public bool CanEdit(string ownerKey) => ownerKey == OwnerKey;
    }

    public static void MapTaxonomyEndpoints(this WebApplication app)
    {
        // ---- Mode thu vien ----------------------------------------------------
        app.MapGet("/api/taxonomy/{kind}", (HttpContext http, MasterPlayContext db, string kind, string? q) =>
            WithLibraryActor(http, actor => List(db, actor, kind, q)));

        app.MapGet("/api/taxonomy/themes/grouped", (HttpContext http, MasterPlayContext db, string? q) =>
            WithLibraryActor(http, actor => Grouped(http, db, actor, q)));

        app.MapPost("/api/taxonomy/{kind}", (HttpContext http, MasterPlayContext db, string kind, TaxonomyItemDto body) =>
            WithLibraryActor(http, actor => Create(db, actor, kind, body)));

        app.MapPut("/api/taxonomy/{kind}/{id:int}", (HttpContext http, MasterPlayContext db, string kind, int id, TaxonomyItemDto body) =>
            WithLibraryActor(http, actor => Update(db, actor, kind, id, body)));

        app.MapDelete("/api/taxonomy/{kind}/{id:int}", (HttpContext http, MasterPlayContext db, string kind, int id) =>
            WithLibraryActor(http, actor => Delete(db, actor, kind, id)));

        app.MapPut("/api/taxonomy/themes/order", (HttpContext http, MasterPlayContext db, ThemeOrderRequest body) =>
            WithLibraryActor(http, actor => OrderThemes(db, actor, body)));

        app.MapPost("/api/taxonomy/theme-groups", (HttpContext http, MasterPlayContext db, ThemeGroupWriteRequest body) =>
            WithLibraryActor(http, actor => CreateGroup(http, db, actor, body)));

        app.MapPut("/api/taxonomy/theme-groups/{id:int}", (HttpContext http, MasterPlayContext db, int id, ThemeGroupWriteRequest body) =>
            WithLibraryActor(http, actor => UpdateGroup(http, db, actor, id, body)));

        app.MapDelete("/api/taxonomy/theme-groups/{id:int}", (HttpContext http, MasterPlayContext db, int id) =>
            WithLibraryActor(http, actor => DeleteGroup(db, actor, id)));

        app.MapPut("/api/taxonomy/theme-groups/order", (HttpContext http, MasterPlayContext db, IdOrderRequest body) =>
            WithLibraryActor(http, actor => OrderGroups(db, actor, body)));

        // ---- Admin Console: chi du lieu 'system:shared' ------------------------
        app.MapGet("/api/admin/taxonomy/themes/grouped", (HttpContext http, MasterPlayContext db, string? q) =>
            WithAdminActor(http, actor => Grouped(http, db, actor, q)));

        app.MapGet("/api/admin/taxonomy/{kind}", (HttpContext http, MasterPlayContext db, string kind, string? q) =>
            WithAdminActor(http, actor => kind == "series" ? Task.FromResult(BadKind(kind)) : List(db, actor, kind, q)));

        app.MapPost("/api/admin/taxonomy/{kind}", (HttpContext http, MasterPlayContext db, string kind, TaxonomyItemDto body) =>
            WithAdminActor(http, actor => kind == "series" ? Task.FromResult(BadKind(kind)) : Create(db, actor, kind, body)));

        app.MapPut("/api/admin/taxonomy/{kind}/{id:int}", (HttpContext http, MasterPlayContext db, string kind, int id, TaxonomyItemDto body) =>
            WithAdminActor(http, actor => kind == "series" ? Task.FromResult(BadKind(kind)) : Update(db, actor, kind, id, body)));

        app.MapDelete("/api/admin/taxonomy/{kind}/{id:int}", (HttpContext http, MasterPlayContext db, string kind, int id) =>
            WithAdminActor(http, actor => kind == "series" ? Task.FromResult(BadKind(kind)) : Delete(db, actor, kind, id)));

        app.MapPut("/api/admin/taxonomy/themes/order", (HttpContext http, MasterPlayContext db, ThemeOrderRequest body) =>
            WithAdminActor(http, actor => OrderThemes(db, actor, body)));

        app.MapPut("/api/admin/taxonomy/genres/order", (HttpContext http, MasterPlayContext db, IdOrderRequest body) =>
            WithAdminActor(http, actor => OrderGenres(db, actor, body)));

        app.MapPost("/api/admin/taxonomy/theme-groups", (HttpContext http, MasterPlayContext db, ThemeGroupWriteRequest body) =>
            WithAdminActor(http, actor => CreateGroup(http, db, actor, body)));

        app.MapPut("/api/admin/taxonomy/theme-groups/{id:int}", (HttpContext http, MasterPlayContext db, int id, ThemeGroupWriteRequest body) =>
            WithAdminActor(http, actor => UpdateGroup(http, db, actor, id, body)));

        app.MapDelete("/api/admin/taxonomy/theme-groups/{id:int}", (HttpContext http, MasterPlayContext db, int id) =>
            WithAdminActor(http, actor => DeleteGroup(db, actor, id)));

        app.MapPut("/api/admin/taxonomy/theme-groups/order", (HttpContext http, MasterPlayContext db, IdOrderRequest body) =>
            WithAdminActor(http, actor => OrderGroups(db, actor, body)));
    }

    // ---- Chu the --------------------------------------------------------------

    // Cung quy tac voi TryGetLibraryOwnerKey / RequireAdmin trong Program.cs (ham
    // cuc bo top-level, khong goi duoc tu day). Doi mot ben thi doi ca ben kia.
    private static Task<IResult> WithLibraryActor(HttpContext http, Func<Actor, Task<IResult>> handler)
    {
        if (http.Items["QuestLogGuest"] is true)
            return handler(new Actor("guest:local"));
        if (http.Items["QuestLogAccount"] is AuthAccountDto account && !string.IsNullOrWhiteSpace(account.Id))
            return handler(new Actor($"user:{account.Id}"));
        return Task.FromResult(Error(401, "AUTH_REQUIRED", "Sign in to continue."));
    }

    private static Task<IResult> WithAdminActor(HttpContext http, Func<Actor, Task<IResult>> handler)
    {
        if (http.Items["QuestLogGuest"] is true)
            return Task.FromResult(Error(403, "ADMIN_REQUIRED", "This action requires an admin account."));
        if (http.Items["QuestLogAccount"] is not AuthAccountDto account || string.IsNullOrWhiteSpace(account.Id))
            return Task.FromResult(Error(401, "AUTH_REQUIRED", "Sign in to continue."));
        if (!string.Equals(account.Role, "admin", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(Error(403, "ADMIN_REQUIRED", "This action requires an admin account."));
        return handler(new Actor(TaxonomyRules.SharedOwnerKey));
    }

    // ---- Doc ------------------------------------------------------------------

    private static async Task<IResult> List(MasterPlayContext db, Actor actor, string kind, string? q)
    {
        var term = TaxonomyRules.NormalizeName(q);
        switch (kind)
        {
            case "series":
                // Series chua owner-scope (ngoai pham vi spec nay): ai cung sua duoc nhu truoc.
                return Results.Ok(await db.Series.AsNoTracking()
                    .Where(s => string.IsNullOrWhiteSpace(q) || s.Name.Contains(q))
                    .OrderBy(s => s.Name)
                    .Select(s => new TaxonomyItemDto(s.Id, s.Name, null, 0, null, null, true, null))
                    .ToListAsync());
            case "genres":
            {
                var rows = await VisibleGenres(db, actor).ToListAsync();
                var visible = TaxonomyRules.Dedupe(rows, g => g.NameKey, g => g.LibraryOwnerKey)
                    .Where(g => term.Length == 0 || g.NameKey.Contains(term))
                    .OrderBy(g => g.SortOrder).ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => ToDto(g, actor.OwnerKey));
                return Results.Ok(visible.ToList());
            }
            case "themes":
            {
                var rows = await VisibleThemes(db, actor).ToListAsync();
                var visible = TaxonomyRules.Dedupe(rows, t => t.NameKey, t => t.LibraryOwnerKey)
                    .Where(t => term.Length == 0 || t.NameKey.Contains(term))
                    .OrderBy(t => t.SortOrder).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(t => ToDto(t, actor.OwnerKey));
                return Results.Ok(visible.ToList());
            }
            default:
                return BadKind(kind);
        }
    }

    private static async Task<IResult> Grouped(HttpContext http, MasterPlayContext db, Actor actor, string? q)
    {
        var language = http.Request.Headers.AcceptLanguage.ToString();
        var term = TaxonomyRules.NormalizeName(q);

        var groups = TaxonomyRules.OrderGroups(await VisibleGroups(db, actor).ToListAsync());
        var themes = TaxonomyRules.Dedupe(
                await VisibleThemes(db, actor).ToListAsync(),
                t => t.NameKey,
                t => t.LibraryOwnerKey)
            .Where(t => term.Length == 0 || t.NameKey.Contains(term))
            .ToList();

        var groupIds = groups.Select(g => g.Id).ToHashSet();
        var buckets = groups
            .Select(group => new ThemeGroupBucketDto(
                ToDto(group, actor.OwnerKey, language),
                TaxonomyRules.OrderThemes(themes.Where(t => t.GroupId == group.Id))
                    .Select(t => ToDto(t, actor.OwnerKey))
                    .ToList()))
            .ToList();

        // Nhom tro toi nhom nguoi goi khong thay (vd. sau khi admin xoa nhom) -> chua phan nhom.
        buckets.Add(new ThemeGroupBucketDto(
            null,
            TaxonomyRules.OrderThemes(themes.Where(t => t.GroupId is null || !groupIds.Contains(t.GroupId.Value)))
                .Select(t => ToDto(t, actor.OwnerKey))
                .ToList()));

        // Co tim kiem: bo nhom rong. Khong tim: giu ca nhom rong (de keo tha vao).
        var result = term.Length > 0
            ? buckets.Where(bucket => bucket.Themes.Count > 0).ToList()
            : buckets;
        return Results.Ok(new GroupedThemesDto(result));
    }

    // ---- Ghi genre/theme --------------------------------------------------------

    private static async Task<IResult> Create(MasterPlayContext db, Actor actor, string kind, TaxonomyItemDto body)
    {
        var name = CleanName(body.Name);
        if (name is null) return Error(400, "TAXONOMY_NAME_REQUIRED", "Name is required.");
        var key = TaxonomyRules.NormalizeName(name);
        var iconData = actor.IsAdmin ? body.IconData : null;

        switch (kind)
        {
            case "series":
            {
                if (await db.Series.AnyAsync(x => x.Name.ToLower() == name.ToLower()))
                    return Error(409, "TAXONOMY_NAME_EXISTS", $"Series already exists: {name}");
                var item = new Series { Name = name };
                db.Series.Add(item);
                await db.SaveChangesAsync();
                return Results.Created($"/api/taxonomy/series/{item.Id}",
                    new TaxonomyItemDto(item.Id, item.Name, null, 0, null, null, true, null));
            }
            case "genres":
            {
                if (await VisibleGenres(db, actor).AnyAsync(x => x.NameKey == key))
                    return Error(409, "TAXONOMY_NAME_EXISTS", $"Genre already exists: {name}");
                var sortOrder = await NextSortOrder(db.Genres.Where(x => x.LibraryOwnerKey == actor.OwnerKey).Select(x => x.SortOrder));
                var item = new Genre { Name = name, IconData = iconData, SortOrder = sortOrder, LibraryOwnerKey = actor.OwnerKey };
                db.Genres.Add(item);
                await db.SaveChangesAsync();
                return Results.Created($"/api/taxonomy/genres/{item.Id}", ToDto(item, actor.OwnerKey));
            }
            case "themes":
            {
                if (await VisibleThemes(db, actor).AnyAsync(x => x.NameKey == key))
                    return Error(409, "TAXONOMY_NAME_EXISTS", $"Theme already exists: {name}");
                if (body.GroupId is int groupId && await FindPlaceableGroup(db, actor, groupId) is null)
                    return Error(400, "THEME_GROUP_INVALID", $"Theme group is not available: {groupId}");
                var sortOrder = await NextSortOrder(db.Themes
                    .Where(x => x.LibraryOwnerKey == actor.OwnerKey && x.GroupId == body.GroupId)
                    .Select(x => x.SortOrder));
                var item = new Theme
                {
                    Name = name,
                    IconData = iconData,
                    SortOrder = sortOrder,
                    LibraryOwnerKey = actor.OwnerKey,
                    GroupId = body.GroupId,
                };
                db.Themes.Add(item);
                await db.SaveChangesAsync();
                return Results.Created($"/api/taxonomy/themes/{item.Id}", ToDto(item, actor.OwnerKey));
            }
            default:
                return BadKind(kind);
        }
    }

    /// <summary>
    /// Doi ten / icon / sortOrder. KHONG doi nhom — chuyen nhom chi qua `…/themes/order`,
    /// de mot client cu gui groupId null khong vo tinh keo theme ra khoi nhom.
    /// </summary>
    private static async Task<IResult> Update(MasterPlayContext db, Actor actor, string kind, int id, TaxonomyItemDto body)
    {
        var name = CleanName(body.Name);
        if (name is null) return Error(400, "TAXONOMY_NAME_REQUIRED", "Name is required.");
        var key = TaxonomyRules.NormalizeName(name);

        switch (kind)
        {
            case "series":
            {
                var item = await db.Series.SingleOrDefaultAsync(x => x.Id == id);
                if (item is null) return Error(404, "TAXONOMY_NOT_FOUND", $"Series not found: {id}");
                if (await db.Series.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower()))
                    return Error(409, "TAXONOMY_NAME_EXISTS", $"Series already exists: {name}");
                item.Name = name;
                await db.SaveChangesAsync();
                return Results.Ok(new TaxonomyItemDto(item.Id, item.Name, null, 0, null, null, true, null));
            }
            case "genres":
            {
                var item = await db.Genres.SingleOrDefaultAsync(x => x.Id == id);
                if (item is null || !actor.CanSee(item.LibraryOwnerKey))
                    return Error(404, "TAXONOMY_NOT_FOUND", $"Genre not found: {id}");
                if (!actor.CanEdit(item.LibraryOwnerKey))
                    return Error(403, "TAXONOMY_NOT_EDITABLE", "This genre is shared and can't be changed here.");
                if (await VisibleGenres(db, actor).AnyAsync(x => x.Id != id && x.NameKey == key))
                    return Error(409, "TAXONOMY_NAME_EXISTS", $"Genre already exists: {name}");
                item.Name = name;
                item.IconData = actor.IsAdmin ? body.IconData : null;
                item.SortOrder = body.SortOrder;
                await db.SaveChangesAsync();
                return Results.Ok(ToDto(item, actor.OwnerKey));
            }
            case "themes":
            {
                var item = await db.Themes.SingleOrDefaultAsync(x => x.Id == id);
                if (item is null || !actor.CanSee(item.LibraryOwnerKey))
                    return Error(404, "TAXONOMY_NOT_FOUND", $"Theme not found: {id}");
                if (!actor.CanEdit(item.LibraryOwnerKey))
                    return Error(403, "TAXONOMY_NOT_EDITABLE", "This theme is shared and can't be changed here.");
                if (await VisibleThemes(db, actor).AnyAsync(x => x.Id != id && x.NameKey == key))
                    return Error(409, "TAXONOMY_NAME_EXISTS", $"Theme already exists: {name}");
                item.Name = name;
                item.IconData = actor.IsAdmin ? body.IconData : null;
                item.SortOrder = body.SortOrder;
                await db.SaveChangesAsync();
                return Results.Ok(ToDto(item, actor.OwnerKey));
            }
            default:
                return BadKind(kind);
        }
    }

    private static async Task<IResult> Delete(MasterPlayContext db, Actor actor, string kind, int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        switch (kind)
        {
            case "series":
            {
                var item = await db.Series.SingleOrDefaultAsync(x => x.Id == id);
                if (item is null) return Error(404, "TAXONOMY_NOT_FOUND", $"Series not found: {id}");
                await db.Games.Where(x => x.SeriesId == id)
                    .ExecuteUpdateAsync(update => update.SetProperty(x => x.SeriesId, (int?)null));
                db.Series.Remove(item);
                break;
            }
            case "genres":
            {
                var item = await db.Genres.SingleOrDefaultAsync(x => x.Id == id);
                if (item is null || !actor.CanSee(item.LibraryOwnerKey))
                    return Error(404, "TAXONOMY_NOT_FOUND", $"Genre not found: {id}");
                if (!actor.CanEdit(item.LibraryOwnerKey))
                    return Error(403, "TAXONOMY_NOT_EDITABLE", "This genre is shared and can't be deleted here.");
                await db.GameGenres.Where(x => x.GenreId == id).ExecuteDeleteAsync();
                db.Genres.Remove(item);
                break;
            }
            case "themes":
            {
                var item = await db.Themes.SingleOrDefaultAsync(x => x.Id == id);
                if (item is null || !actor.CanSee(item.LibraryOwnerKey))
                    return Error(404, "TAXONOMY_NOT_FOUND", $"Theme not found: {id}");
                if (!actor.CanEdit(item.LibraryOwnerKey))
                    return Error(403, "TAXONOMY_NOT_EDITABLE", "This theme is shared and can't be deleted here.");
                await db.GameThemes.Where(x => x.ThemeId == id).ExecuteDeleteAsync();
                db.Themes.Remove(item);
                break;
            }
            default:
                return BadKind(kind);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> OrderThemes(MasterPlayContext db, Actor actor, ThemeOrderRequest body)
    {
        var ids = (body.Ids ?? []).Distinct().ToList();
        if (body.GroupId is int groupId && await FindPlaceableGroup(db, actor, groupId) is null)
            return Error(400, "THEME_GROUP_INVALID", $"Theme group is not available: {groupId}");

        var themes = await db.Themes.Where(x => ids.Contains(x.Id)).ToListAsync();
        if (themes.Count != ids.Count || themes.Any(t => !actor.CanSee(t.LibraryOwnerKey)))
            return Error(404, "TAXONOMY_NOT_FOUND", "One or more themes no longer exist.");
        if (themes.Any(t => !actor.CanEdit(t.LibraryOwnerKey)))
            return Error(403, "TAXONOMY_NOT_EDITABLE", "Shared themes can't be moved here.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        var byId = themes.ToDictionary(t => t.Id);
        for (var index = 0; index < ids.Count; index++)
        {
            byId[ids[index]].GroupId = body.GroupId;
            byId[ids[index]].SortOrder = index;
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> OrderGenres(MasterPlayContext db, Actor actor, IdOrderRequest body)
    {
        var ids = (body.Ids ?? []).Distinct().ToList();
        var genres = await db.Genres.Where(x => ids.Contains(x.Id)).ToListAsync();
        if (genres.Count != ids.Count || genres.Any(g => !actor.CanSee(g.LibraryOwnerKey)))
            return Error(404, "TAXONOMY_NOT_FOUND", "One or more genres no longer exist.");
        if (genres.Any(g => !actor.CanEdit(g.LibraryOwnerKey)))
            return Error(403, "TAXONOMY_NOT_EDITABLE", "Shared genres can't be reordered here.");

        var byId = genres.ToDictionary(g => g.Id);
        for (var index = 0; index < ids.Count; index++)
            byId[ids[index]].SortOrder = index;

        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // ---- Nhom theme ---------------------------------------------------------------

    private static async Task<IResult> CreateGroup(HttpContext http, MasterPlayContext db, Actor actor, ThemeGroupWriteRequest body)
    {
        if (!TaxonomyRules.TryResolveGroupNames(body.NameEn, body.NameVi, actor.IsAdmin, out var nameEn, out var nameVi))
            return Error(400, "THEME_GROUP_NAME_REQUIRED",
                actor.IsAdmin ? "Enter both the English and Vietnamese names." : "Enter a group name.");

        var sortOrder = body.SortOrder ?? await NextSortOrder(db.ThemeGroups
            .Where(g => g.LibraryOwnerKey == actor.OwnerKey)
            .Select(g => g.SortOrder));
        var group = new ThemeGroup { LibraryOwnerKey = actor.OwnerKey, NameEn = nameEn, NameVi = nameVi, SortOrder = sortOrder };
        db.ThemeGroups.Add(group);
        await db.SaveChangesAsync();
        return Results.Created($"/api/taxonomy/theme-groups/{group.Id}",
            ToDto(group, actor.OwnerKey, http.Request.Headers.AcceptLanguage.ToString()));
    }

    private static async Task<IResult> UpdateGroup(HttpContext http, MasterPlayContext db, Actor actor, int id, ThemeGroupWriteRequest body)
    {
        var group = await db.ThemeGroups.SingleOrDefaultAsync(g => g.Id == id);
        if (group is null || !actor.CanSee(group.LibraryOwnerKey))
            return Error(404, "THEME_GROUP_NOT_FOUND", $"Theme group not found: {id}");
        if (!actor.CanEdit(group.LibraryOwnerKey))
            return Error(403, "TAXONOMY_NOT_EDITABLE", "This group is shared and can't be changed here.");
        if (!TaxonomyRules.TryResolveGroupNames(body.NameEn, body.NameVi, actor.IsAdmin, out var nameEn, out var nameVi))
            return Error(400, "THEME_GROUP_NAME_REQUIRED",
                actor.IsAdmin ? "Enter both the English and Vietnamese names." : "Enter a group name.");

        group.NameEn = nameEn;
        group.NameVi = nameVi;
        if (body.SortOrder is int sortOrder) group.SortOrder = sortOrder;
        await db.SaveChangesAsync();
        return Results.Ok(ToDto(group, actor.OwnerKey, http.Request.Headers.AcceptLanguage.ToString()));
    }

    private static async Task<IResult> DeleteGroup(MasterPlayContext db, Actor actor, int id)
    {
        var group = await db.ThemeGroups.SingleOrDefaultAsync(g => g.Id == id);
        if (group is null || !actor.CanSee(group.LibraryOwnerKey))
            return Error(404, "THEME_GROUP_NOT_FOUND", $"Theme group not found: {id}");
        if (!actor.CanEdit(group.LibraryOwnerKey))
            return Error(403, "TAXONOMY_NOT_EDITABLE", "This group is shared and can't be deleted here.");

        // I4: moi theme trong nhom (ke ca theme ca nhan cua owner khac trong nhom chung)
        // ve chua phan nhom. Ghi tuong minh, khong dua vao FK ON DELETE SET NULL.
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Themes.Where(t => t.GroupId == id)
            .ExecuteUpdateAsync(update => update.SetProperty(t => t.GroupId, (int?)null));
        db.ThemeGroups.Remove(group);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> OrderGroups(MasterPlayContext db, Actor actor, IdOrderRequest body)
    {
        var ids = (body.Ids ?? []).Distinct().ToList();
        var groups = await db.ThemeGroups.Where(g => ids.Contains(g.Id)).ToListAsync();
        if (groups.Count != ids.Count || groups.Any(g => !actor.CanSee(g.LibraryOwnerKey)))
            return Error(404, "THEME_GROUP_NOT_FOUND", "One or more theme groups no longer exist.");
        if (groups.Any(g => !actor.CanEdit(g.LibraryOwnerKey)))
            return Error(403, "TAXONOMY_NOT_EDITABLE", "Shared groups can't be reordered here.");

        var byId = groups.ToDictionary(g => g.Id);
        for (var index = 0; index < ids.Count; index++)
            byId[ids[index]].SortOrder = index;

        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // ---- Tro giup -------------------------------------------------------------

    private static IQueryable<Theme> VisibleThemes(MasterPlayContext db, Actor actor) =>
        db.Themes.AsNoTracking().Where(t => t.LibraryOwnerKey == TaxonomyRules.SharedOwnerKey || t.LibraryOwnerKey == actor.OwnerKey);

    private static IQueryable<Genre> VisibleGenres(MasterPlayContext db, Actor actor) =>
        db.Genres.AsNoTracking().Where(g => g.LibraryOwnerKey == TaxonomyRules.SharedOwnerKey || g.LibraryOwnerKey == actor.OwnerKey);

    private static IQueryable<ThemeGroup> VisibleGroups(MasterPlayContext db, Actor actor) =>
        db.ThemeGroups.AsNoTracking().Where(g => g.LibraryOwnerKey == TaxonomyRules.SharedOwnerKey || g.LibraryOwnerKey == actor.OwnerKey);

    /// <summary>Nhom ma theme cua Actor duoc dat vao (I1/I2), hoac null.</summary>
    private static async Task<ThemeGroup?> FindPlaceableGroup(MasterPlayContext db, Actor actor, int groupId)
    {
        var group = await VisibleGroups(db, actor).SingleOrDefaultAsync(g => g.Id == groupId);
        return group is not null && TaxonomyRules.CanPlaceTheme(actor.OwnerKey, group.LibraryOwnerKey) ? group : null;
    }

    private static async Task<int> NextSortOrder(IQueryable<int> sortOrders) =>
        await sortOrders.AnyAsync() ? await sortOrders.MaxAsync() + 1 : 0;

    private static string? CleanName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Scope(string ownerKey) =>
        TaxonomyRules.IsShared(ownerKey) ? "shared" : "personal";

    public static TaxonomyItemDto ToDto(Theme theme, string actorOwnerKey) =>
        new(theme.Id, theme.Name, theme.IconData, theme.SortOrder,
            theme.NameKey, Scope(theme.LibraryOwnerKey), theme.LibraryOwnerKey == actorOwnerKey, theme.GroupId);

    public static TaxonomyItemDto ToDto(Genre genre, string actorOwnerKey) =>
        new(genre.Id, genre.Name, genre.IconData, genre.SortOrder,
            genre.NameKey, Scope(genre.LibraryOwnerKey), genre.LibraryOwnerKey == actorOwnerKey, null);

    private static ThemeGroupDto ToDto(ThemeGroup group, string actorOwnerKey, string? language) =>
        new(group.Id, Scope(group.LibraryOwnerKey), group.NameEn, group.NameVi,
            TaxonomyRules.GroupDisplayName(group, language), group.SortOrder,
            group.LibraryOwnerKey == actorOwnerKey);

    private static IResult BadKind(string kind) =>
        Error(400, "BAD_KIND", $"kind khong hop le: {kind}");

    private static IResult Error(int status, string code, string message) =>
        Results.Json(new { error = new { code, message } }, statusCode: status);
}
