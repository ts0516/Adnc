﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Adnc.Usr.Application.Dtos;
using Adnc.Application.Shared.Interceptors;
using Adnc.Application.Shared.Services;
using Adnc.Infr.EasyCaching.Interceptor.Castle;
using Adnc.Common.Consts;

namespace Adnc.Usr.Application.Services
{
    public interface IDeptAppService : IAppService
    {
        Task<List<DeptNodeDto>> GetList();

        [OpsLog(LogName = "新增/修改部门")]
        [EasyCachingEvict(CacheKey = EasyCachingConsts.DetpListCacheKey)]
        Task Save(DeptSaveInputDto savetDto);

        [OpsLog(LogName = "删除部门")]
        [EasyCachingEvict(CacheKey = EasyCachingConsts.DetpListCacheKey)]
        Task Delete(long Id);
    }
}
