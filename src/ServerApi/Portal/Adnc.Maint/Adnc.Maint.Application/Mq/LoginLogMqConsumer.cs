﻿using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Adnc.Maint.Core.Entities;
using Adnc.Core.Shared.IRepositories;
using Adnc.Infr.Mq.RabbitMq;
using Adnc.Common.Consts;

namespace Adnc.Maint.Application.Mq
{
    public sealed class LoginLogMqConsumer : BaseRabbitMqConsumer
    {
        // 因为Process函数是委托回调,直接将其他Service注入的话两者不在一个scope,
        // 这里要调用其他的Service实例只能用IServiceProvider CreateScope后获取实例对象
        private readonly IServiceProvider _services;
        private readonly ILogger<LoginLogMqConsumer> _logger;

        public LoginLogMqConsumer(IOptionsSnapshot<RabbitMqConfig> options
           , IHostApplicationLifetime appLifetime
           , ILogger<LoginLogMqConsumer> logger
           , IServiceProvider services)
            : base(options, appLifetime, logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override ExchageConfig GetExchageConfig()
        {
            return new ExchageConfig()
            {
                Name = MqConsts.Exchanges.Logs
                ,
                Type = ExchangeType.Direct
                ,
                DeadExchangeName = MqConsts.Exchanges.Dead
            };
        }

        protected override string[] GetRoutingKeys()
        {
            return new[] { MqConsts.RoutingKeys.Loginlog }; 
        }

        protected override QueueConfig GetQueueConfig()
        {
            var config = GetCommonQueueConfig();

            config.Name = "q-adnc-maint-loginlog";
            config.AutoAck = false;
            config.PrefetchCount = 5;
            config.Arguments = new Dictionary<string, object>()
                  {
                     //设置当前队列的DLX
                    { "x-dead-letter-exchange",MqConsts.Exchanges.Dead} 
                    //设置DLX的路由key，DLX会根据该值去找到死信消息存放的队列
                    ,{ "x-dead-letter-routing-key",MqConsts.RoutingKeys.Loginlog}
                    //设置消息的存活时间，即过期时间(毫秒)
                    ,{ "x-message-ttl",1000*60}
                  };
            return config;
        }

        protected async override Task<bool> Process(string exchage, string routingKey, string message)
        {
            bool result = false;
            try
            {
                using (var scope = _services.CreateScope())
                {
                    var _loginLogRepository = scope.ServiceProvider.GetRequiredService<IEfRepository<SysLoginLog>>();
                    var entity = JsonSerializer.Deserialize<SysLoginLog>(message);
                    await _loginLogRepository.InsertAsync(entity);
                    result = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
            return result;
        }
    }
}
