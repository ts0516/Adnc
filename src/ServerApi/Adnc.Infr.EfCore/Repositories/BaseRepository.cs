﻿using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Z.EntityFramework.Plus;
using Dapper;
using Adnc.Common.Extensions;
using Adnc.Core.Shared.IRepositories;
using Adnc.Common.Models;
using Adnc.Core.Shared.Entities;

namespace Adnc.Infr.EfCore.Repositories
{
    public abstract class BaseRepository<TDbContext, TEntity> : IEfRepository<TEntity>
       where TDbContext : DbContext
       where TEntity : EfEntity
    {
        protected virtual TDbContext DbContext { get; }

        public BaseRepository(TDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public virtual IQueryable<TrdEntity> GetAll<TrdEntity>() where TrdEntity : EfEntity
        {
            return DbContext.Set<TrdEntity>();
        }

        public virtual IQueryable<TEntity> GetAll()
        {
            return DbContext.Set<TEntity>();
        }

        public virtual async Task<IEnumerable<dynamic>> QueryAsync(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            return await DbContext.Database.GetDbConnection().QueryAsync(sql, param, null, commandTimeout, commandType);
        }

        public virtual async Task<IEnumerable<TrEntity>> QueryAsync<TrEntity>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            return await DbContext.Database.GetDbConnection().QueryAsync<TrEntity>(sql, param, null, commandTimeout, commandType);
        }

        public virtual async Task<int> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            await DbContext.Set<TEntity>().AddAsync(entity);
            return await DbContext.SaveChangesAsync(cancellationToken);
        }

        public virtual async Task<int> InsertRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            await DbContext.Set<TEntity>().AddRangeAsync(entities);
            return await DbContext.SaveChangesAsync(cancellationToken);
        }

        public virtual async Task<int> DeleteAsync(long[] keyValues, CancellationToken cancellationToken = default)
        {
            //if (keyValues.Length > 1)
            //    return await DbContext.Set<TEntity>().Where(e => keyValues.Contains(e.ID)).DeleteAsync(cancellationToken);
            //else
            //    return await DbContext.Set<TEntity>().Where(e => e.ID == keyValues[0]).DeleteAsync(cancellationToken);
            var mapping = DbContext.Model.FindEntityType(typeof(TEntity)); //3.0
            var schema = mapping.GetSchema() ?? "dbo";
            var tableName = mapping.GetTableName();
            var keyNames = mapping.GetProperties().Where(p => p.IsPrimaryKey()).Select(p => p.PropertyInfo.Name);

            if (keyNames.Count() > 1 || keyValues?.Length < 1)
                return 0;

            string keyName = keyNames.First();
            string sql = string.Empty;

            if (keyValues.Length > 1)
            {
                sql = $"delete from {tableName} where {keyName} in {(string.Join(",", keyValues))};";
            }
            else
            {
                sql = $"delete from {tableName} where {keyName}={keyValues[0]};";
            }
            return await DbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }

        public virtual async Task<int> DeleteRangeAsync(Expression<Func<TEntity, bool>> whereExpression, CancellationToken cancellationToken= default)
        {
            return await DbContext.Set<TEntity>().Where(whereExpression).DeleteAsync(cancellationToken);
        }

        public async virtual Task<int> UpdateAsync(TEntity entity, params Expression<Func<TEntity, object>>[] propertyExpressions)
        {
            if (propertyExpressions == null || propertyExpressions.Length == 0)
            {
                DbContext.Update(entity);
            }
            else
            {
                var entry = DbContext.GetEntityEntry(entity, out var existBefore);

                if (existBefore)
                {
                    var propNames = propertyExpressions.Select(x => x.GetMemberName()).ToArray();

                    foreach (var propEntry in entry.Properties)
                    {
                        if (!propNames.Contains(propEntry.Metadata.Name))
                        {
                            propEntry.IsModified = false;
                        }
                    }
                }
                else
                {
                    entry.State = EntityState.Unchanged;
                    foreach (var expression in propertyExpressions)
                    {
                        entry.Property(expression).IsModified = true;
                    }
                }
            }
            return await DbContext.SaveChangesAsync(default);
        }

        public virtual async Task<int> UpdateRangeAsync(Expression<Func<TEntity, bool>> whereExpression,Expression<Func<TEntity, TEntity>> upDateExpression, CancellationToken cancellationToken = default)
        {
            var mapping = DbContext.Model.FindEntityType(typeof(TEntity));
            var property = mapping.GetProperties().Where(p => p.Name == "RowVersion").FirstOrDefault();

            if (property != null)
            {
                throw new Exception("该实体有RowVersion列，不能使用批量更新");
            }

            

            return await DbContext.Set<TEntity>().Where(whereExpression).UpdateAsync(upDateExpression,cancellationToken);
        }

        public virtual async Task<bool> ExistAsync(Expression<Func<TEntity, bool>> whereExpression, CancellationToken cancellationToken = default)
        {
            return await DbContext.Set<TEntity>().AnyAsync(whereExpression, cancellationToken);
        }

        public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>> whereExpression, CancellationToken cancellationToken = default)
        {
            return await DbContext.Set<TEntity>().CountAsync(whereExpression);
        }

        public virtual async Task<TEntity> FindAsync(object[] keyValues, CancellationToken cancellationToken = default)
        {
            return await DbContext.Set<TEntity>().FindAsync(keyValues, cancellationToken);
        }

        public virtual async Task<TEntity> FetchAsync<TResult>(Expression<Func<TEntity, TResult>> selector, Expression<Func<TEntity, bool>> whereExpression, Expression<Func<TEntity, object>> orderByExpression = null, bool ascending = false, CancellationToken cancellationToken = default)
        {
            dynamic result;
            
            var query = DbContext.Set<TEntity>().Where(whereExpression).AsNoTracking();

            if (orderByExpression == null)
            {
                ///result = await query.Select(selector).FirstOrDefaultAsync();
                result = await query.Select(selector).FirstOrDefaultAsync();
            }
            else
            {
                result = ascending
                          ? await query.OrderBy(orderByExpression).Select(selector).FirstOrDefaultAsync()
                          : await query.OrderByDescending(orderByExpression).Select(selector).FirstOrDefaultAsync()
                          ;
            }

            if (result == null)
                return null;

            return (typeof(TEntity) == typeof(TResult))
                ? await Task.FromResult(result as TEntity)
                : await Task.FromResult(JsonSerializer.Deserialize<TEntity>(JsonSerializer.Serialize(result)))
                ;
        }

        public virtual async Task<List<TResult>> SelectAsync<TResult>(Expression<Func<TEntity, TResult>> selector, Expression<Func<TEntity, bool>> whereExpression, Expression<Func<TEntity, object>> orderByExpression = null, bool ascending = false, CancellationToken cancellationToken = default)
        {
            var query = DbContext.Set<TEntity>().AsNoTracking().Where(whereExpression);
            if (orderByExpression != null)
            {
                if (ascending)
                {
                    query = query.OrderBy(orderByExpression);
                }
                else
                {
                    query = query.OrderByDescending(orderByExpression);
                }
            }

            return await query.Select(selector).ToListAsync();
        }

        public virtual async Task<List<TResult>> SelectAsync<TResult>(int count, Expression<Func<TEntity, TResult>> selector, Expression<Func<TEntity, bool>> whereExpression, Expression<Func<TEntity, object>> orderByExpression=null, bool ascending = false, CancellationToken cancellationToken = default)
        {
            var query = DbContext.Set<TEntity>().AsNoTracking().Where(whereExpression);
            if (orderByExpression != null)
            {
                if (ascending)
                {
                    query = query.OrderBy(orderByExpression);
                }
                else
                {
                    query = query.OrderByDescending(orderByExpression);
                }
            }

            return await query.Select(selector).Take(count).ToListAsync(cancellationToken);
        }

        public virtual async Task<IPagedModel<TEntity>> PagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, bool>> whereExpression, Expression<Func<TEntity, object>> orderByExpression, bool ascending = false, CancellationToken cancellationToken = default)
        {
            var total = await DbContext.Set<TEntity>().AsNoTracking().CountAsync(whereExpression, cancellationToken);
            if (total == 0)
            {
                return new PagedModel<TEntity>() { PageSize = pageSize };
            }

            if (pageIndex <= 0)
            {
                pageIndex = 1;
            }

            if (pageSize <= 0)
            {
                pageSize = 10;
            }

            var query = DbContext.Set<TEntity>().AsNoTracking()
                .Where(whereExpression);
            query = ascending ? query.OrderBy(orderByExpression) : query.OrderByDescending(orderByExpression);
            var data = await query.Skip((pageIndex - 1) * pageSize)
                                  .Take(pageSize)
                                  .ToArrayAsync(cancellationToken);
            return new PagedModel<TEntity>()
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = total,
                Data = data
            };
        }

        protected EntityEntry<TEntity> Entry(TEntity entity)
        {
            return DbContext.Entry<TEntity>(entity);
        }
    }
}
