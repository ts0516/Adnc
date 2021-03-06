﻿using AutoMapper;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Adnc.Core.Shared.IRepositories;
using Adnc.Maint.Core.Entities;
using Adnc.Maint.Application.Dtos;
using Adnc.Common.Extensions;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Collections.Generic;
using Adnc.Core.Maint.Entities;
using Adnc.Application.Shared.Dtos;

namespace  Adnc.Maint.Application.Services
{
    public class LogAppService : ILogAppService
    {
        private readonly IMapper _mapper;
        private readonly IMongoRepository<SysOperationLog> _opsLogRepository;
        private readonly IMongoRepository<SysNloglog> _nlogLogRepository;
        private readonly IEfRepository<SysLoginLog> _loginLogRepository;

        public LogAppService(IMapper mapper,
            IMongoRepository<SysOperationLog> opsLogRepository
            , IEfRepository<SysLoginLog> loginLogRepository
            , IMongoRepository<SysNloglog> nlogLogRepository)
       {
            _mapper = mapper;
            _opsLogRepository = opsLogRepository;
            _loginLogRepository = loginLogRepository;
            _nlogLogRepository = nlogLogRepository;
        }

        public async Task<PageModelDto<LoginLogDto>> GetLoginLogsPaged(LogSearchDto searchDto)
        {

            Expression<Func<SysLoginLog, bool>> whereCondition = x => true;

            if (searchDto.BeginTime.HasValue)
            {
                whereCondition = whereCondition.And(x => x.CreateTime >= searchDto.BeginTime.Value);
            }

            if (searchDto.EndTime.HasValue)
            {
                whereCondition = whereCondition.And(x => x.CreateTime <= searchDto.EndTime.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchDto.Account))
            {
                whereCondition = whereCondition.And(x => x.Account == searchDto.Account);
            }

            if (!string.IsNullOrWhiteSpace(searchDto.Device))
            {
                whereCondition = whereCondition.And(x => x.Device == searchDto.Device);
            }

            var pagedModel = await _loginLogRepository.PagedAsync(searchDto.PageIndex, searchDto.PageSize, whereCondition, x => x.CreateTime, false);

            var result = _mapper.Map<PageModelDto<LoginLogDto>>(pagedModel);

            return result;
        }


        public async Task<PageModelDto<OpsLogDto>> GetOpsLogsPaged(LogSearchDto searchDto)
        {
            var builder = Builders<SysOperationLog>.Filter;
            var filters = new List<FilterDefinition<SysOperationLog>>();

            if (searchDto.BeginTime.HasValue)
            {
                filters.Add(builder.Gte(l => l.CreateTime, searchDto.BeginTime));
            }

            if (searchDto.EndTime.HasValue)
            {
                filters.Add(builder.Lte(l => l.CreateTime, searchDto.EndTime));
            }

            if (!string.IsNullOrWhiteSpace(searchDto.Account))
            {
                filters.Add(builder.Eq(l => l.Account, searchDto.Account));
            }

            if (!string.IsNullOrWhiteSpace(searchDto.Method))
            {
                filters.Add(builder.Eq(l => l.Method, searchDto.Method));
            }

            var filter = filters.Count > 0 ? builder.And(filters) : builder.Where(x => true);

            var pagedModel = await _opsLogRepository.PagedAsync(searchDto.PageIndex, searchDto.PageSize, filter, x => x.CreateTime, false);

            var result = _mapper.Map<PageModelDto<OpsLogDto>>(pagedModel);

            return result;
        }

        public async Task<PageModelDto<NlogLogDto>> GetNlogLogsPaged(LogSearchDto searchDto)
        {
            var builder = Builders<SysNloglog>.Filter;
            var filters = new List<FilterDefinition<SysNloglog>>();

            if (searchDto.BeginTime.HasValue)
            {
                filters.Add(builder.Gte(l => l.Date, searchDto.BeginTime));
            }

            if (searchDto.EndTime.HasValue)
            {
                filters.Add(builder.Lte(l => l.Date, searchDto.EndTime));
            }

            //if (!string.IsNullOrWhiteSpace(searchDto.Account))
            //{
            //    filters.Add(builder.Eq(l => l.Properties., searchDto.Account));
            //}

            if (!string.IsNullOrWhiteSpace(searchDto.Method))
            {
                filters.Add(builder.Eq(l => l.Properties.Method, searchDto.Method));
            }

            var filter = filters.Count > 0 ? builder.And(filters) : builder.Where(x => true);

            var pagedModel = await _nlogLogRepository.PagedAsync(searchDto.PageIndex, searchDto.PageSize, filter, x => x.Date, false);

            var result = _mapper.Map<PageModelDto<NlogLogDto>>(pagedModel);

            return result;
        }
    }
}
