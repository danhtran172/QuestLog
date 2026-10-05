using MasterPlay.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MasterPlay.Infrastructure.Integrations.Images
{
    /// <summary>
    /// Thu vien nao luu anh o dau. Chi thu vien cua tai khoan role <c>admin</c>
    /// len Cloudinary; moi chu khac (user, guest, legacy) giu anh tren may
    /// (<see cref="LocalImageStore"/>). DOMAIN_RULES §12.
    ///
    /// Role chi biet duoc luc Host xac thuc token, nhung Tray va lenh CLI cung
    /// dong bo anh ma khong co ai dang nhap — nen Host ghi role vao bang
    /// <c>ImageStorageOwners</c> trong SQLite moi khi thay no doi.
    ///
    /// Tai khoan (<c>user:*</c>) chua tung duoc ghi nhan thi KHONG BIET: nguoi goi
    /// phai bo qua thu vien do, khong duoc doan. Doan "local" se keo anh Cloudinary
    /// cua admin ve may; doan "cloud" se day anh cua user len dam may.
    /// Guest va legacy khong bao gio la admin nen luon luu tren may.
    /// </summary>
    public static class ImageStorageOwners
    {
        /// <summary>Gia tri da ghi xuong CSDL trong tien trinh nay — tranh ghi lai moi request.</summary>
        private static readonly ConcurrentDictionary<string, bool> Recorded = new(StringComparer.Ordinal);

        private sealed record Row(string LibraryOwnerKey, bool UsesCloud);

        /// <summary>Ghi nhan role hien tai cua mot tai khoan. Chi ghi CSDL khi doi.</summary>
        public static async Task RecordAsync(string ownerKey, bool isAdmin, CancellationToken cancellationToken = default)
        {
            if (Recorded.TryGetValue(ownerKey, out var known) && known == isAdmin) return;

            await using var context = new MasterPlayContext();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO ImageStorageOwners (LibraryOwnerKey, UsesCloud, RecordedAt)
                VALUES ({ownerKey}, {(isAdmin ? 1 : 0)}, {DateTime.UtcNow.ToString("O")})
                ON CONFLICT(LibraryOwnerKey) DO UPDATE SET UsesCloud = excluded.UsesCloud, RecordedAt = excluded.RecordedAt
                """,
                cancellationToken);
            Recorded[ownerKey] = isAdmin;
        }

        /// <summary>
        /// <c>true</c> = Cloudinary, <c>false</c> = tren may, <c>null</c> = tai khoan
        /// chua tung dang nhap tren Host nay. Doc CSDL moi lan: Tray la tien trinh khac.
        /// </summary>
        public static async Task<bool?> UsesCloudAsync(string ownerKey, CancellationToken cancellationToken = default)
        {
            await using var context = new MasterPlayContext();
            var stored = await context.Database
                .SqlQuery<int>($"SELECT UsesCloud AS Value FROM ImageStorageOwners WHERE LibraryOwnerKey = {ownerKey}")
                .ToListAsync(cancellationToken);
            return Resolve(stored.Count == 0 ? null : stored[0] != 0, ownerKey);
        }

        /// <summary>Toan bo bang, cho dong bo hang loat. Tra cuu bang <see cref="Resolve(IReadOnlyDictionary{string,bool},string)"/>.</summary>
        public static async Task<IReadOnlyDictionary<string, bool>> LoadAsync(CancellationToken cancellationToken = default)
        {
            await using var context = new MasterPlayContext();
            var rows = await context.Database
                .SqlQuery<Row>($"SELECT LibraryOwnerKey, UsesCloud FROM ImageStorageOwners")
                .ToListAsync(cancellationToken);
            return rows.ToDictionary(row => row.LibraryOwnerKey, row => row.UsesCloud, StringComparer.Ordinal);
        }

        public static bool? Resolve(IReadOnlyDictionary<string, bool> owners, string ownerKey) =>
            Resolve(owners.TryGetValue(ownerKey, out var usesCloud) ? usesCloud : null, ownerKey);

        private static bool? Resolve(bool? stored, string ownerKey)
        {
            if (stored.HasValue) return stored.Value;
            // Tai khoan chua ghi nhan: khong biet. Con lai (guest, legacy) khong co role -> tren may.
            return ownerKey.StartsWith("user:", StringComparison.Ordinal) ? null : false;
        }
    }
}
