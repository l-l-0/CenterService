//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.Data;
using HealthCare.Models;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;

#pragma warning disable CS1591

namespace HealthCare.CenterService.Controllers
{
    /// <summary>
    ///     用户登录认证权限等API
    /// </summary>
    [UserAuthorize]
    public class authController : BaseController
    {
        /// <summary>
        ///     获取终端已经登录的用户
        /// </summary>
        [HttpGet]
        [ActionName("authorized-users")]
        public List<AuthorizedUser> AuthorizedUsers(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            return ServiceStartup.GetAuthorized(Terminal);
        }

        /// <summary>
        ///     确认操作人的验证，只会登录是否验证通过，不做权限验证
        /// </summary>
        [HttpGet]
        [ActionName("certify")]
        public bool Certify(string user, string password)
        {
            if (string.Equals(user, ServiceStartup.Kernel, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var pwd = Helper.ComputeMd5Hash(password, new User { UniqueId = user, });
            return mongo.UserCollection.AsQueryable().Any(u => u.LoginId.ToLower() == user.ToLower() && u.Password == pwd);
        }

        /// <summary>
        ///     用户登录验证, -404:用户不存在，-400:密码错误，-403:用户被禁用，-409:用户重复登录
        /// </summary>
        /// <param name="user">登录名</param>
        /// <param name="password">密码</param>
        /// <param name="terminal"></param>
        [HttpGet]
        [ActionName("login")]
        [AllowAnonymous]
        public async Task<LoginResult> LoginAsync(string user, string password, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            if (string.Equals(user, ServiceStartup.Kernel, StringComparison.OrdinalIgnoreCase))
            {
                var primary = new AuthorizedUserCache { UniqueId = SfraObject.EmptyId(), LoginId = ServiceStartup.Kernel, DisplayName = "Kernel User", Kernel = true, Token = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff") };
                await ServiceStartup.SetPrimaryAuthorizedAsync(Terminal, primary);
                return new LoginResult { Code = LoginCode.Ok, Jwt = primary.Token, Ip = Terminal, };
            }
            var find = mongo.UserCollection.AsQueryable().FirstOrDefault(u => u.LoginId.ToLower() == user.ToLower());
            var code = LoginCode.Ok;
            if (find == null)
            {
                code = LoginCode.NotExist;
            }
            else if (find.IsDisabled)
            {
                code = LoginCode.UserIsDenied;
            }
            else if (!find.CanPasswordAuth)
            {
                code = LoginCode.PwdAuthDenied;
            }
            else if (Helper.ComputeMd5Hash(password, find) != find.Password)
            {
                code = LoginCode.PasswordError;
            }
            return await TryLoginAsync(code, find, Terminal);
        }

        /// <summary>
        ///     指纹登录 （供本地服务调用）
        /// </summary>
        [HttpGet]
        [ActionName("finger-login")]
        [AllowAnonymous]
        public async Task<LoginResult> FingerLoginAsync(string user, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            // 不安全。可以让客户端传一个校验码
            var find = mongo.UserCollection.AsQueryable().FirstOrDefault(u => u.LoginId == user);
            var code = LoginCode.Ok;
            if (find == null)
            {
                code = LoginCode.NotExist;
            }
            else if (find.IsDisabled)
            {
                code = LoginCode.UserIsDenied;
            }
            else if (!find.CanPrintfingerAuth)
            {
                code = LoginCode.FingerAuthDenied;
            }
            return await TryLoginAsync(code, find, Terminal);
        }
        /// <summary>
        ///     人脸识别登录 （供本地服务调用）
        /// </summary>
        [HttpGet]
        [ActionName("face-login")]
        [AllowAnonymous]
        public async Task<LoginResult> FaceLoginAsync(string user, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            // 不安全。可以让客户端传一个校验码
            var find = mongo.UserCollection.AsQueryable().FirstOrDefault(u => u.LoginId == user);
            var code = LoginCode.Ok;
            if (find == null)
            {
                code = LoginCode.NotExist;
            }
            else if (find.IsDisabled)
            {
                code = LoginCode.UserIsDenied;
            }
            else if (!find.CanFaceAuth)
            {
                code = LoginCode.FaceAuthDenied;
            }
            return await TryLoginAsync(code, find, Terminal);
        }

        private async Task<LoginResult> TryLoginAsync(LoginCode code, User find, string ip)
        {
            if (code == LoginCode.Ok)
            {
                // 默认单人登录
                var single = (mongo.SystemConfigCollection.AsQueryable().Where(s => s.Key == $"{ip}:SingleAuth").Select(s => s.JObject).FirstOrDefault() ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);
                var isFirstUser = false;
                var primary = ServiceStartup.GetPrimaryAuthorized(ip);
                var token = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff");

                var menuId = mongo.RoleCollection.AsQueryable().FirstOrDefault(r => r.Users.Contains(find.UniqueId) && r.DefaultMenu != null)?.DefaultMenu ?? null;
                var menu = mongo.MenuCollection.AsQueryable().FirstOrDefault(f => f.UniqueId == menuId);
                var defaultMenu = menu == null ? null : $"/{menu.ParentId}/{menu.Uri}";

                if (primary == null)
                {
                    isFirstUser = true;
                    primary = new AuthorizedUserCache
                    {
                        UniqueId = find.UniqueId,
                        LoginId = find.LoginId,
                        DisplayName = find.Employee?.DisplayName ?? find.LoginId,
                        Kernel = false,
                        Token = token,
                        DefaultMenu = defaultMenu,
                    };
                    await ServiceStartup.SetPrimaryAuthorizedAsync(ip, primary);
                }

                if (single)
                {
                    await mongo.AccessJournalCollection.InsertOneAsync(new AccessJournal { Computer = ip, UserId = primary.LoginId, UserName = primary.DisplayName, });
                    return new LoginResult { Code = code, Jwt = token, Menu = defaultMenu, Ip = ip, };
                }

                if (!isFirstUser)
                {
                    if (find.LoginId == primary.LoginId)
                    {
                        return new LoginResult { Code = LoginCode.DuplicateLogin, Ip = ip, };
                    }

                    await ServiceStartup.SetSecondaryAuthorizedAsync(ip, new AuthorizedUserCache
                    {
                        UniqueId = find.UniqueId,
                        LoginId = find.LoginId,
                        DisplayName = find.Employee?.DisplayName ?? find.LoginId,
                        Kernel = false,
                        Token = token,
                        DefaultMenu = defaultMenu,
                    });
                    await mongo.AccessJournalCollection.InsertOneAsync(new AccessJournal { Computer = ip, UserId = primary.LoginId, UserName = primary.DisplayName, });
                    await mongo.AccessJournalCollection.InsertOneAsync(new AccessJournal { Computer = ip, UserId = find.LoginId, UserName = find.DisplayName, });

                    return new LoginResult { Code = code, Jwt = token, Menu = defaultMenu, Ip = ip, };
                }
            }

            return new LoginResult { Code = code, Ip = ip, };
        }

        /// <summary>
        ///     退出登录
        /// </summary>
        [HttpGet]
        [ActionName("logout")]
        [AllowAnonymous]
        public async Task<bool> LogoutAsync()
        {
            var users = ServiceStartup.GetAuthorized(Terminal);
            if (users.Any())
            {
                var jors = users.Select(x => new AccessJournal { Computer = Terminal, UserId = x.UserId, UserName = x.UserName, }).ToList();
                await mongo.AccessJournalCollection.InsertManyAsync(jors);
                await ServiceStartup.ClearAuthorizedAsync(Terminal);
            }
            return users.Any();
        }

        /// <summary>
        ///     导航菜单
        /// </summary>
        public class MenuModel
        {
            [JsonIgnore]
            public string Id { get; set; }
            public string Path { get; set; }
            public Data Data { get; set; }
            public MenuModel[] Children { get; set; }
        }

        public class Data
        {
            public NgMenu Menu { get; set; }
        }

        public class NgMenu
        {
            public string Title { get; set; }
            public string Icon { get; set; }
            public bool Selected { get; set; }
            public bool Expanded { get; set; }
            public int Order { get; set; }
        }

        /// <summary>
        ///     获取登录用户的组合菜单
        /// </summary>
        [HttpGet]
        [ActionName("navigation-for-certify")]
        public MenuModel NavigationForCertify(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            var primary = ServiceStartup.GetPrimaryAuthorized(Terminal);
            var secondary = ServiceStartup.GetSecondaryAuthorized(Terminal);
            var kernel = primary.Kernel ? true : secondary?.Kernel ?? false;

            var pId = primary.LoginId;
            var sId = secondary?.LoginId;
            var pMenus = mongo.UserCollection.AsQueryable().Where(u => !u.IsDisabled && u.LoginId == pId).SelectMany(u => u.Menus).ToList();
            var sMenus = mongo.UserCollection.AsQueryable().Where(u => !u.IsDisabled && u.LoginId == sId).SelectMany(u => u.Menus).ToList();

            var hospital = MongoData.Helper.GetHospital();
            // 根据不同的医院，决定
            // 菜单为登录人的交集还是并集
            var keys = new[] { "UnKnown", }.Contains(hospital) ? sId != null ? pMenus.Intersect(sMenus).ToList() : pMenus : pMenus.Union(sMenus).ToList();

            var menus = mongo.MenuCollection.AsQueryable().Where(m => !m.IsDisabled).OrderBy(m => m.DisplayOrder).ToList();
            var node = new MenuModel
            {
                Path = "pages",
                Children = menus.Where(m => m.IsModule).Select(m => new MenuModel
                {
                    Id = m.UniqueId,
                    Path = m.Uri,
                    Data = new Data
                    {
                        Menu = new NgMenu { Expanded = false, Selected = false, Order = m.DisplayOrder, Title = m.DisplayName, Icon = m.Icon, },
                    },
                    Children = menus.Where(x => x.ParentId == m.UniqueId && (kernel || keys.Any(y => y == x.UniqueId))).Select(x => new MenuModel
                    {
                        Id = x.UniqueId,
                        Path = x.Uri,
                        Data = new Data
                        {
                            Menu = new NgMenu { Expanded = false, Selected = false, Order = x.DisplayOrder, Title = x.DisplayName, Icon = x.Icon, }
                        },
                    }).ToArray(),
                }).Where(m => m.Children.Any()).ToArray(),
            };
            return node;
        }

        /// <summary>
        ///     修改指定用户的登录密码。 -1 用户未找到； -2 用户无密码认证权限； -3 旧密码错误； 0 修改成功
        /// </summary>
        [HttpGet]
        [ActionName("modify-password")]
        [AllowAnonymous]
        public async Task<int> ModifyPasswordAsync(string login, string old, string current)
        {
            var user = mongo.UserCollection.AsQueryable().FirstOrDefault(f => f.LoginId == login);
            if (user == null)
            {
                return -1;
            }

            if (!user.CanPasswordAuth)
            {
                return -2;
            }

            if (Helper.ComputeMd5Hash(old, user) != user.Password)
            {
                return -3;
            }

            user.Password = Helper.ComputeMd5Hash(current, user);
            await mongo.UserCollection.UpdateOneAsync(u => u.UniqueId == user.UniqueId, Builders<User>.Update.Set(u => u.Password, user.Password));
            return 0;
        }

        /// <summary>
        ///     校验监督人的药品使用权限
        /// </summary>
        [HttpPost]
        [ActionName("supervisor-goods-permission")]
        public async Task<string[]> SupervisorGoodsPermissionAsync(string user, [FromBody] string[] goods, string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            if (ServiceStartup.GetAuthorized(Terminal).Any(a => a.UserId == user))
            {
                // 监督人不能是登录人
                return goods;
            }

            // 添加主登陆人的药盒权限?
            var find = mongo.UserCollection.AsQueryable().FirstOrDefault(u => u.LoginId == user);
            var boxes = find?.AvailableStorages ?? new List<string>();
            var goodsIds = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).Where(c => c.Computer == Terminal).ToList()
                .SelectMany(c => c.Drawers).SelectMany(d => d.Boxes).Join(boxes, a => a.No, b => b, (a, b) => a).SelectMany(b => b.Fills).Select(f => f.GoodsId).Distinct().ToList();
            var cannots = goods.Except(goodsIds).ToArray();
            if (!cannots.Any())
            {
                await ServiceStartup.SetCertifyAuthorizedAsync(Terminal, new AuthorizedUserCache
                {
                    Kernel = false,
                    UniqueId = find.UniqueId,
                    LoginId = find.LoginId,
                    DisplayName = find.Employee?.DisplayName,
                });
            }

            return cannots;
        }

        /// <summary>
        ///     清除监督人
        /// </summary>
        [HttpGet]
        [ActionName("clear-certify")]
        public async Task<bool> ClearCertifyAsync(string terminal = null)
        {
            Terminal = terminal ?? Terminal;
            await ServiceStartup.ClearCertifyAuthorizedAsync(Terminal);
            return true;
        }

        /// <summary>
        ///     更新指纹特征
        /// </summary>
        [HttpPut]
        [ActionName("modify-fingerprint")]
        public async Task<bool> ModifyFingerprintAsync(string user, [FromBody] string[] fingerprint)
        {
            var find = mongo.UserCollection.AsQueryable().FirstOrDefault(u => u.LoginId == user);
            if (find == null)
            {
                return false;
            }

            find.Fingerprint = fingerprint;
            await mongo.UserCollection.UpdateOneAsync(x => x.UniqueId == find.UniqueId, Builders<User>.Update.Set(f => f.Fingerprint, find.Fingerprint));

            // 更新服务端指纹版本
            var version = Helper.NowStringValue;
            var config = new SystemConfig { UniqueId = Helper.FingerVersion, Key = Helper.FingerVersion, JObject = version, };
            await mongo.SystemConfigCollection.FindOneAndReplaceAsync<SystemConfig>(x => x.UniqueId == Helper.FingerVersion, config, new FindOneAndReplaceOptions<SystemConfig, SystemConfig> { IsUpsert = true });

#pragma warning disable CS4014
            Task.Run(() =>
            {
                // 指纹特征修改后，发送到客户端
                var users = mongo.UserCollection.AsQueryable().Select(u => new { u.LoginId, u.Fingerprint, u.Employee, }).ToList();
                var fingers = users.Select(u => new FingerIdentity
                {
                    UserId = u.LoginId,
                    UserName = u.Employee?.DisplayName ?? u.LoginId,
                    Templates = u.Fingerprint,
                }).ToArray();
                var computers = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).ToList().Select(c => c.Computer).Distinct().ToList();
                Parallel.ForEach(computers, (ip) =>
                {
                    var url = $"http://{ip}:8002/api/bizcore/finger-templates?version={version}";
                    try
                    {
                        var b = new HttpRequest().Put<bool>(url, fingers);
                        Global.GlobalLogger.Info($"{url} sended => {b}");
                    }
                    catch (Exception ex)
                    {
                        Global.GlobalLogger.Error(url, ex);
                    }
                });
            });
#pragma warning restore CS4014

            return true;
        }

        /// <summary>
        ///     更新人脸特征
        /// </summary>
        [HttpPut]
        [ActionName("modify-facerecognition")]
        public async Task<bool> ModifyFaceRecognitionAsync(string user, [FromBody] string face)
        {
            var find = mongo.UserCollection.AsQueryable().FirstOrDefault(u => u.LoginId == user);
            if (find == null)
            {
                return false;
            }

            find.Face = face;
            await mongo.UserCollection.UpdateOneAsync(x => x.UniqueId == find.UniqueId, Builders<User>.Update.Set(f => f.Face, find.Face));

            // 更新服务端人脸版本
            var version = Helper.NowStringValue;
            var config = new SystemConfig { UniqueId = Helper.FaceVersion, Key = Helper.FaceVersion, JObject = version, };
            await mongo.SystemConfigCollection.FindOneAndReplaceAsync<SystemConfig>(x => x.UniqueId == Helper.FaceVersion, config, new FindOneAndReplaceOptions<SystemConfig, SystemConfig> { IsUpsert = true });

#pragma warning disable CS4014
            Task.Run(() =>
            {
                // 人脸特征修改后，发送到客户端
                var users = mongo.UserCollection.AsQueryable().Select(u => new { u.LoginId, u.Face, u.Employee, }).ToList();
                var faces = users.Select(u => new FaceIdentity
                {
                    UserId = u.LoginId,
                    UserName = u.Employee?.DisplayName ?? u.LoginId,
                    Template = u.Face,
                }).ToArray();
                var computers = mongo.CustomerCollection.AsQueryable().Where(c => !c.IsDisabled).SelectMany(c => c.Cabinets).ToList().Select(c => c.Computer).Distinct().ToList();
                Parallel.ForEach(computers, (ip) =>
                {
                    var url = $"http://{ip}:8002/api/bizcore/face-recognition?version={version}";
                    try
                    {
                        var b = new HttpRequest().Put<bool>(url, faces);
                        Global.GlobalLogger.Info($"{url} sended => {b}");
                    }
                    catch (Exception ex)
                    {
                        Global.GlobalLogger.Error(url, ex);
                    }
                });
            });
#pragma warning restore CS4014

            return true;
        }

    }
}