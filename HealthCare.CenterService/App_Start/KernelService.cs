//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using HealthCare.Data;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#pragma warning disable CS1591

namespace HealthCare.CenterService
{
    public static class ServiceStartup
    {
        private const string CacheKey = "zzzServiceStartup.KernelCache";

        private static MongoContext mongo = new MongoContext();

        public class UserAuthentication : SfraObject
        {
            public AuthorizedUserCache User { get; set; }
        }

        internal static TimeSpan AuthenticationExpiration => TimeSpan.FromMinutes(Global.AppSettings["sframed:AuthenticationExpiration"].Value<double>());

        internal static string Kernel => Global.AppSettings["sframed:Kernel"].Value<string>();

        private static string PrimaryCacheKey(string terminal) => $"{terminal}:Authorized@primary";

        private static string SecondaryCacheKey(string terminal) => $"{terminal}:Authorized@secondary";

        private static string CertifyCacheKey(string terminal) => $"{terminal}:Authorized@certify";

        public static List<AuthorizedUser> GetAuthorized(string terminal)
        {
            var pKey = PrimaryCacheKey(terminal);
            var sKey = SecondaryCacheKey(terminal);

            var flag = DateTime.Now.Add(-AuthenticationExpiration);
            return mongo.AnyCollection<UserAuthentication>(CacheKey).AsQueryable().Where(x => x.CreatedTime >= flag && (x.UniqueId == pKey || x.UniqueId == sKey))
                .Select(x => new AuthorizedUser
                {
                    UserId = x.User.LoginId,
                    UserName = x.User.DisplayName,
                    Kernel = x.User.Kernel,
                    Token = x.User.Token,
                    DefaultMenu = x.User.DefaultMenu,
                }).ToList();
        }

        public static async Task ClearAuthorizedAsync(string terminal)
        {
            var keys = new[] { PrimaryCacheKey(terminal), SecondaryCacheKey(terminal), CertifyCacheKey(terminal), };
            await mongo.AnyCollection<UserAuthentication>(CacheKey).DeleteManyAsync(o => keys.Contains(o.UniqueId));
        }

        public static void RefreshAuthorized(string terminal)
        {
            var flag = DateTime.Now.Add(-AuthenticationExpiration);
            // 只能刷新未过期的登录记录
            foreach (var k in new[] { PrimaryCacheKey(terminal), SecondaryCacheKey(terminal), })
            {
                mongo.AnyCollection<UserAuthentication>(CacheKey).UpdateOne(x => x.UniqueId == k && x.CreatedTime >= flag, Builders<UserAuthentication>.Update.Set(x => x.CreatedTime, DateTime.Now));
            }
        }

        public static AuthorizedUserCache GetPrimaryAuthorized(string terminal)
        {
            var key = PrimaryCacheKey(terminal);
            var flag = DateTime.Now.Add(-AuthenticationExpiration);
            return mongo.AnyCollection<UserAuthentication>(CacheKey).AsQueryable().FirstOrDefault(f => f.CreatedTime >= flag && f.UniqueId == key)?.User;
        }

        public static AuthorizedUserCache GetSecondaryAuthorized(string terminal)
        {
            var key = SecondaryCacheKey(terminal);
            var flag = DateTime.Now.Add(-AuthenticationExpiration);
            return mongo.AnyCollection<UserAuthentication>(CacheKey).AsQueryable().FirstOrDefault(f => f.CreatedTime >= flag && f.UniqueId == key)?.User;
        }

        public static AuthorizedUserCache GetCertifyAuthorized(string terminal)
        {
            var key = CertifyCacheKey(terminal);
            var flag = DateTime.Now.Add(-AuthenticationExpiration);
            return mongo.AnyCollection<UserAuthentication>(CacheKey).AsQueryable().FirstOrDefault(f => f.CreatedTime >= flag && f.UniqueId == key)?.User;
        }

        /// <summary>
        ///     缓存主登录人
        /// </summary>
        public static async Task SetPrimaryAuthorizedAsync(string terminal, AuthorizedUserCache value)
        {
            var data = new UserAuthentication { UniqueId = PrimaryCacheKey(terminal), User = value, };
            await mongo.AnyCollection<UserAuthentication>(CacheKey).FindOneAndReplaceAsync<UserAuthentication>(x => x.UniqueId == data.UniqueId, data, new FindOneAndReplaceOptions<UserAuthentication, UserAuthentication> { IsUpsert = true });
        }

        /// <summary>
        ///     缓存次登录人
        /// </summary>
        public static async Task SetSecondaryAuthorizedAsync(string terminal, AuthorizedUserCache value)
        {
            var data = new UserAuthentication { UniqueId = SecondaryCacheKey(terminal), User = value, };
            await mongo.AnyCollection<UserAuthentication>(CacheKey).FindOneAndReplaceAsync<UserAuthentication>(x => x.UniqueId == data.UniqueId, data, new FindOneAndReplaceOptions<UserAuthentication, UserAuthentication> { IsUpsert = true });
        }

        /// <summary>
        ///     缓存监督人
        /// </summary>
        public static async Task SetCertifyAuthorizedAsync(string terminal, AuthorizedUserCache value)
        {
            var data = new UserAuthentication { UniqueId = CertifyCacheKey(terminal), User = value, };
            await mongo.AnyCollection<UserAuthentication>(CacheKey).FindOneAndReplaceAsync<UserAuthentication>(x => x.UniqueId == data.UniqueId, data, new FindOneAndReplaceOptions<UserAuthentication, UserAuthentication> { IsUpsert = true });
        }

        public static async Task ClearCertifyAuthorizedAsync(string terminal)
        {
            var key = CertifyCacheKey(terminal);
            await mongo.AnyCollection<UserAuthentication>(CacheKey).DeleteOneAsync(x => x.UniqueId == key);
        }
    }

    public class AuthorizedUserCache
    {
        [JsonProperty("_id")]
        [BsonElement("_id")]
        public string UniqueId { get; set; }
        public string LoginId { get; set; }
        public string DisplayName { get; set; }
        public bool Kernel { get; set; }
        public string Token { get; set; }
        public string DefaultMenu { get; set; }
    }

    public class AuthorizedUser
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public bool Kernel { get; set; }
        public string Token { get; set; }
        public string DefaultMenu { get; set; }
    }
}