﻿//------------------------------------------------------------------------------
// <autogenerated>
//     This code was generated by a CodeSmith Template.
//
//     DO NOT MODIFY contents of this file. Changes to this
//     file will be lost if the code is regenerated.
//     Author:Yubao Li
// </autogenerated>
//------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using OpenAuth.Repository.Core;

namespace OpenAuth.Repository.Domain
{
    /// <summary>
	/// 系统授权规制表
	/// </summary>
    [Table("DataPrivilegeRule")]
    public partial class DataPrivilegeRule : Entity
    {
        public DataPrivilegeRule()
        {
          this.SourceCode= string.Empty;
          this.SubSourceCode= string.Empty;
          this.Description= string.Empty;
          this.SortNo= 0;
          this.PrivilegeRules= string.Empty;
          this.CreateTime= DateTime.Now;
          this.CreateUserId= string.Empty;
          this.CreateUserName= string.Empty;
          this.UpdateTime= DateTime.Now;
          this.UpdateUserId= string.Empty;
          this.UpdateUserName= string.Empty;
        }

        /// <summary>
        /// 资源标识（模块编号）
        /// </summary>
        public string SourceCode { get; set; }
        /// <summary>
        /// 二级资源标识
        /// </summary>
        public string SubSourceCode { get; set; }
        /// <summary>
        /// 权限描述
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// 排序号
        /// </summary>
        public int SortNo { get; set; }
        /// <summary>
        /// 权限规则
        /// </summary>
        public string PrivilegeRules { get; set; }
        /// <summary>
        /// 是否可用
        /// </summary>
        public byte Enable { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>
        public System.DateTime CreateTime { get; set; }
        /// <summary>
        /// 创建人ID
        /// </summary>
        public string CreateUserId { get; set; }
        /// <summary>
        /// 创建人
        /// </summary>
        public string CreateUserName { get; set; }
        /// <summary>
        /// 最后更新时间
        /// </summary>
        public System.DateTime? UpdateTime { get; set; }
        /// <summary>
        /// 最后更新人ID
        /// </summary>
        public string UpdateUserId { get; set; }
        /// <summary>
        /// 最后更新人
        /// </summary>
        public string UpdateUserName { get; set; }
    }
}