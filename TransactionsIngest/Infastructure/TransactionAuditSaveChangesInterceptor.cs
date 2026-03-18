using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace TransactionsIngest.Data.Interceptors;

public sealed class TransactionAuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private const string RevisionPropertyName = "Revision";
    private const string UpdatedAtUtcPropertyName = nameof(Transaction.UpdatedAtUtc);
    private const string TransactionEntityIdPropertyName = nameof(TransactionAudit.TransactionEntityId);
    private const string InsertChangeType = "Insert";
    private const string UpdateChangeType = "Update";
    private const string DeleteChangeType = "Delete";

    private static readonly HashSet<string> IgnoredChangedFieldNames = new(StringComparer.Ordinal)
    {
        UpdatedAtUtcPropertyName,
        RevisionPropertyName
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ApplyAuditEntries(DbContext? context)
    {
        if (context is not IngestDbContext db)
            return;

        var nowUtc = DateTime.UtcNow;
        var audits = new List<TransactionAudit>();

        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (entry.Entity is TransactionAudit)
                continue;

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            if (entry.State is EntityState.Added or EntityState.Modified)
                StampCommonPersistenceFields(entry, nowUtc);

            if (entry.Entity is Transaction transaction)
                ProcessTransaction(entry, transaction, nowUtc, audits);
        }

        if (audits.Count > 0)
            db.TransactionAudits.AddRange(audits);
    }

    private static void StampCommonPersistenceFields(EntityEntry entry, DateTime nowUtc)
    {
        var updatedAt = entry.Properties.FirstOrDefault(p => p.Metadata.Name == UpdatedAtUtcPropertyName);
        if (updatedAt is not null)
            updatedAt.CurrentValue = nowUtc;

        var revision = entry.Properties.FirstOrDefault(p => p.Metadata.Name == RevisionPropertyName);
        if (revision?.Metadata.ClrType != typeof(int))
            return;

        if (entry.State == EntityState.Added)
        {
            var current = revision.CurrentValue as int? ?? 0;
            revision.CurrentValue = current > 0 ? current : 1;
            return;
        }

        if (entry.State == EntityState.Modified)
        {
            var original = revision.OriginalValue as int? ?? 0;
            revision.CurrentValue = original + 1;
        }
    }

    private static void ProcessTransaction(
        EntityEntry entry,
        Transaction transaction,
        DateTime nowUtc,
        ICollection<TransactionAudit> audits)
    {
        if (entry.State == EntityState.Deleted)
        {
            var beforeValues = BuildSnapshot(entry, useOriginalValue: true);

            audits.Add(new TransactionAudit
            {
                TransactionId = transaction.TransactionId,
                TransactionEntityId = transaction.Id,
                Revision = GetDeleteRevision(entry),
                ChangeType = DeleteChangeType,
                ChangedFields = null,
                BeforeChanges = SerializeAsJson(beforeValues),
                AfterChanges = null,
                RecordedAtUtc = nowUtc
            });
            return;
        }

        if (entry.State == EntityState.Added)
        {
            var revision = GetCurrentRevision(entry);
            var afterValues = BuildSnapshot(entry, useOriginalValue: false);

            audits.Add(new TransactionAudit
            {
                TransactionId = transaction.TransactionId,
                Revision = revision,
                ChangeType = InsertChangeType,
                ChangedFields = null,
                BeforeChanges = null,
                AfterChanges = SerializeAsJson(afterValues),
                RecordedAtUtc = nowUtc,
                Transaction = transaction
            });
            return;
        }

        var modifiedProperties = GetModifiedAuditableProperties(entry).ToList();
        var changedFields = modifiedProperties.Select(p => p.Metadata.Name).ToList();

        if (changedFields.Count == 0)
            return;

        var beforeValuesForChangedFields = BuildSnapshot(modifiedProperties, useOriginalValue: true);
        var afterValuesForChangedFields = BuildSnapshot(modifiedProperties, useOriginalValue: false);

        var changeType = ResolveTransactionChangeType();
        var revisionValue = GetCurrentRevision(entry);

        audits.Add(new TransactionAudit
        {
            TransactionId = transaction.TransactionId,
            TransactionEntityId = transaction.Id,
            Revision = revisionValue,
            ChangeType = changeType,
            ChangedFields = string.Join(", ", changedFields),
            BeforeChanges = SerializeAsJson(beforeValuesForChangedFields),
            AfterChanges = SerializeAsJson(afterValuesForChangedFields),
            RecordedAtUtc = nowUtc,
        });
    }

    private static string? SerializeAsJson(Dictionary<string, object?> values)
    {
        return values.Count == 0 ? null : JsonSerializer.Serialize(values, JsonOptions);
    }

    private static Dictionary<string, object?> BuildSnapshot(EntityEntry entry, bool useOriginalValue)
    {
        return BuildSnapshot(
            entry.Properties.Where(p => p.Metadata.Name != TransactionEntityIdPropertyName),
            useOriginalValue);
    }

    private static Dictionary<string, object?> BuildSnapshot(IEnumerable<PropertyEntry> properties, bool useOriginalValue)
    {
        return properties.ToDictionary(
            p => p.Metadata.Name,
            p => useOriginalValue ? p.OriginalValue : p.CurrentValue);
    }

    private static IEnumerable<PropertyEntry> GetModifiedAuditableProperties(EntityEntry entry)
    {
        return entry.Properties.Where(p => p.IsModified && !IgnoredChangedFieldNames.Contains(p.Metadata.Name));
    }

    private static int GetDeleteRevision(EntityEntry entry)
    {
        var revision = entry.Properties.FirstOrDefault(p => p.Metadata.Name == RevisionPropertyName);
        var original = revision?.OriginalValue as int? ?? 0;
        return original + 1;
    }

    private static int GetCurrentRevision(EntityEntry entry)
    {
        var revision = entry.Properties.FirstOrDefault(p => p.Metadata.Name == RevisionPropertyName);
        return revision?.CurrentValue as int? ?? 1;
    }

    private static string ResolveTransactionChangeType()
    {
        return UpdateChangeType;
    }
}