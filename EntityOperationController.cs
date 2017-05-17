/*******************************************************
 * 
 * 作者：吴中坡
 * 创建日期：20161014
 * 说明：此文件只包含一个类，具体内容见类型注释。
 * 运行环境：.NET 4.0
 * 版本号：1.0.0
 * 
 * 历史记录：
 * 创建文件 吴中坡 20161014 11:29
 * 
*******************************************************/


using Rafy;
using Rafy.Domain;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Web.Http;

namespace DBEN.DBI.Web.Controllers.Api
{
    /// <summary>
    /// 统一封装的前台与后台交互的API
    /// </summary>
    public class EntityOperationController : ApiControllerBase
    {
        private static readonly ConcurrentDictionary<string, EntityControllerConfig> domainControllerDic =
            new ConcurrentDictionary<string, EntityControllerConfig>();
        [HttpPost]
        public Result Execute([FromBody] EntityParameter entityParameter)
        {
            var result = new Result { Success = true };
            try
            {
                var entityControllerConfig = EntityControllerCache(entityParameter);
                object[] paras = null;
                var parameterInfos = entityControllerConfig.Parameters;
                var paramsLength = parameterInfos.Length;
                if (paramsLength > 0)
                {
                    if (paramsLength != entityParameter.Parameters.Length)
                    {
                        throw new ArgumentException(
                            $"Controller：{entityParameter.ControllerName}，Action：{entityParameter.ActionName},调用的参数不匹配");
                    }
                    paras = new object[paramsLength];
                    var entityParameters = entityParameter.Parameters;
                    for (var i = 0; i < paramsLength; i++)
                    {
                        var paramType = parameterInfos[i].ParameterType;
                        if (paramType != typeof(string) && (paramType.IsClass || paramType.IsInterface))
                        {
                            if (paramType.IsArray)
                            {
                                if (typeof(EntityList).IsAssignableFrom(paramType))
                                {
                                    paras[i] = DeserializeList(paramType, Convert.ToString(entityParameters[i]));
                                }
                                else
                                {
                                    paras[i] =
                                        Newtonsoft.Json.JsonConvert.DeserializeObject(
                                            Convert.ToString(entityParameters[i]), paramType);
                                }
                            }
                            else
                            {
                                if (typeof(Entity).IsAssignableFrom(paramType))
                                {
                                    paras[i] = Deserialize(paramType, Convert.ToString(entityParameters[i]));
                                }
                                else
                                {
                                    paras[i] =
                                        Newtonsoft.Json.JsonConvert.DeserializeObject(
                                            Convert.ToString(entityParameters[i]), paramType);
                                }
                            }
                        }
                        else if (paramType.IsEnum)
                            paras[i] = Enum.ToObject(paramType, entityParameters[i]);
                        else
                            paras[i] = Convert.ChangeType(entityParameters[i], paramType);
                    }
                }
                result.Data = entityControllerConfig.Action.Invoke(entityControllerConfig.DomainController, paras);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
            }
            return result;
        }

        /// <summary>
        ///     缓存Controller
        /// </summary>
        /// <param name="entityParameter"></param>
        /// <returns></returns>
        private EntityControllerConfig EntityControllerCache(EntityParameter entityParameter)
        {
            EntityControllerConfig entityControllerConfig;
            string key = $"{entityParameter.ControllerName}_{entityParameter.ActionName}";
            if (!domainControllerDic.TryGetValue(key, out entityControllerConfig))
            {
                entityControllerConfig = new EntityControllerConfig();
                var plugs = RafyEnvironment.DomainPlugins;
                Type controllerType = null;
                foreach (var plug in plugs)
                {
                    controllerType = plug.Assembly.GetType(entityParameter.ControllerName);
                    if (controllerType != null)
                    {
                        break;
                    }
                }
                if (controllerType == null)
                {
                    throw new ArgumentException("错误的Controller：" + entityParameter.ControllerName);
                }
                entityControllerConfig.DomainController = DomainControllerFactory.Create(controllerType);
                var action = controllerType.GetMethod(entityParameter.ActionName);
                if (action == null)
                {
                    throw new ArgumentException(
                        $"Controller：{entityParameter.ControllerName}，不存在对应的Action：{entityParameter.ActionName}");
                }
                var paramsInfo = action.GetParameters();
                entityControllerConfig.Action = action;
                entityControllerConfig.Parameters = paramsInfo;
                domainControllerDic[key] = entityControllerConfig;
            }
            return entityControllerConfig;
        }
    }

    internal class EntityControllerConfig
    {
        public DomainController DomainController { get; set; }
        public MethodInfo Action { get; set; }
        public ParameterInfo[] Parameters { get; set; }
    }
    /// <summary>
    /// 参数
    /// </summary>
    public class EntityParameter
    {
        public string ControllerName { get; set; }
        public string ActionName { get; set; }
        public object[] Parameters { get; set; }
    }
}