﻿using System;
using System.Collections.Generic;
using System.Linq;
using Infrastructure;
using Newtonsoft.Json.Linq;
using OpenAuth.App.Flow;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.App.SSO;
using OpenAuth.Repository.Domain;
using OpenAuth.Repository.Interface;

namespace OpenAuth.App
{
    /// <summary>
    /// 工作流实例表操作
    /// </summary>
    public class FlowInstanceApp : BaseApp<FlowInstance>
    {
        private RevelanceManagerApp _revelanceApp;

        private IAuth _auth;

        #region 流程处理API
        /// <summary>
        /// 创建一个实例
        /// </summary>
        /// <returns></returns>
        public bool CreateInstance(JObject obj)
        {
            var flowInstance = obj.ToObject<FlowInstance>();

            //获取提交的表单数据
            var frmdata = new JObject();
            foreach (var property in obj.Properties().Where(U => U.Name.Contains("data_")))
            {
                frmdata[property.Name] = property.Value;
            }
            flowInstance.FrmData = JsonHelper.Instance.Serialize(frmdata);

            //创建运行实例
            var wfruntime = new FlowRuntime(flowInstance);
            var user = _auth.GetCurrentUser();

            #region 根据运行实例改变当前节点状态
            flowInstance.ActivityId = wfruntime.nextNodeId;
            flowInstance.ActivityType = wfruntime.GetNextNodeType();//-1无法运行,0会签开始,1会签结束,2一般节点,4流程运行结束
            flowInstance.ActivityName = wfruntime.nextNode.name;
            flowInstance.PreviousId = wfruntime.currentNodeId;
            flowInstance.CreateUserId = user.User.Id;
            flowInstance.CreateUserName = user.User.Account;
            flowInstance.MakerList = (wfruntime.GetNextNodeType() != 4 ? GetMakerList(wfruntime) : "");//当前节点可执行的人信息
            flowInstance.IsFinish = (wfruntime.GetNextNodeType() == 4 ? 1 : 0);

            UnitWork.Add(flowInstance);
            #endregion

            #region 流程操作记录
            FlowInstanceOperationHistory processOperationHistoryEntity = new FlowInstanceOperationHistory
            {
                InstanceId = flowInstance.Id,
                CreateUserId = user.User.Id,
                CreateUserName = user.User.Name,
                CreateDate = DateTime.Now,
                Content = "【创建】"
                          + user.User.Name
                          + "创建了一个流程进程【"
                          + flowInstance.Code + "/"
                          + flowInstance.CustomName + "】"
            };
            UnitWork.Add(processOperationHistoryEntity);
            #endregion

            AddTransHistory(user.User, wfruntime);

            return true;
        }

        /// <summary>
        /// 节点审核
        /// </summary>
        /// <param name="instanceId"></param>
        /// <returns></returns>
        public bool NodeVerification(string instanceId, bool flag, string description = "")
        {
            var user = _auth.GetCurrentUser().User;
            FlowInstance flowInstance = Get(instanceId);
            FlowInstanceOperationHistory flowInstanceOperationHistory = new FlowInstanceOperationHistory
            {
                InstanceId = instanceId,
                CreateUserId = user.Id,
                CreateUserName = user.Name,
                CreateDate = DateTime.Now
            };//操作记录
            FlowRuntime wfruntime = new FlowRuntime(flowInstance);

            var tag = new Tag
            {
                UserName = user.Name,
                UserId = user.Id,
                Description = description
            };
            #region 会签
            if (flowInstance.ActivityType == 0)//当前节点是会签节点
            {
                tag.Taged = 1;
                wfruntime.MakeTagNode(wfruntime.currentNodeId, tag);//标记会签节点状态
                
                string verificationNodeId = ""; //寻找当前登录用户可审核的节点Id
                List<string> nodelist = wfruntime.GetCountersigningNodeIdList(wfruntime.currentNodeId);
                foreach (string item in nodelist)
                {
                    var makerList = GetMakerList(wfruntime.nodes[item]);
                    if (makerList == "-1") continue;

                    if (makerList.Split(',').Any(one => user.Id == one))
                    {
                        verificationNodeId = item;
                    }
                }

                if (verificationNodeId != "")
                {
                    if (flag)
                    {
                        tag.Taged = 1;
                        flowInstanceOperationHistory.Content = "【" + wfruntime.nodes[verificationNodeId].name + "】【" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "】同意,备注：" + description;
                    }
                    else
                    {
                        tag.Taged = -1;
                        flowInstanceOperationHistory.Content = "【" + wfruntime.nodes[verificationNodeId].name + "】【" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "】不同意,备注：" + description;
                    }

                    wfruntime.MakeTagNode(verificationNodeId, tag);//标记审核节点状态
                    string confluenceres = wfruntime.NodeConfluence(verificationNodeId, tag);
                    switch (confluenceres)
                    {
                        case "-1"://不通过
                            flowInstance.IsFinish = 3;
                            break;
                        case "1"://等待，当前节点还是会签开始节点，不跳转
                            break;
                        default://通过
                            flowInstance.PreviousId = flowInstance.ActivityId;
                            flowInstance.ActivityId = wfruntime.nextNodeId;
                            flowInstance.ActivityType = wfruntime.nextNodeType;//-1无法运行,0会签开始,1会签结束,2一般节点,4流程运行结束
                            flowInstance.ActivityName = wfruntime.nextNode.name;
                            flowInstance.IsFinish = (wfruntime.nextNodeType == 4 ? 1 : 0);
                            flowInstance.MakerList = (wfruntime.nextNodeType == 4 ? "" : GetMakerList(wfruntime));//当前节点可执行的人信息

                            AddTransHistory(user, wfruntime);

                            break;
                    }
                }
                else
                {
                    throw (new Exception("审核异常,找不到审核节点"));
                }
            }
            #endregion

            #region 一般审核
            else//一般审核
            {
                if (flag)
                {
                    tag.Taged = 1;
                    wfruntime.MakeTagNode(wfruntime.currentNodeId, tag);
                    flowInstance.PreviousId = flowInstance.ActivityId;
                    flowInstance.ActivityId = wfruntime.nextNodeId;
                    flowInstance.ActivityType = wfruntime.nextNodeType;
                    flowInstance.ActivityName = wfruntime.nextNode.name;
                    flowInstance.MakerList = wfruntime.nextNodeType == 4 ? "" : GetMakerList(wfruntime);//当前节点可执行的人信息
                    flowInstance.IsFinish = (wfruntime.nextNodeType == 4 ? 1 : 0);

                    AddTransHistory(user, wfruntime);

                    flowInstanceOperationHistory.Content = "【" + wfruntime.currentNode.name 
                        + "】【" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "】同意,备注：" + description;
                }
                else
                {
                    flowInstance.IsFinish = 3; //表示该节点不同意
                    tag.Taged = -1;
                    wfruntime.MakeTagNode(wfruntime.currentNodeId, tag);

                    flowInstanceOperationHistory.Content = "【" 
                        + wfruntime.currentNode.name + "】【"
                        + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "】不同意,备注："
                        + description;
                }
            }
            #endregion

            flowInstance.SchemeContent = JsonHelper.Instance.Serialize(wfruntime.ToSchemeObj());

            UnitWork.Update(flowInstance);
            UnitWork.Add(flowInstanceOperationHistory);
            UnitWork.Save();
            return true;
        }

        /// <summary>
        /// 驳回
        /// </summary>
        /// <returns></returns>
        public bool NodeReject(VerificationReq reqest)
        {
            var user = _auth.GetCurrentUser().User;

            FlowInstance flowInstance = Get(reqest.FlowInstanceId);
         
            FlowRuntime wfruntime = new FlowRuntime(flowInstance);

            string resnode = "";
            resnode = string.IsNullOrEmpty(reqest.NodeRejectStep) ? wfruntime.RejectNode() : reqest.NodeRejectStep;

            var tag = new Tag
            {
                Description = reqest.VerificationOpinion,
                Taged = 0,
                UserId = user.Id,
                UserName = user.Name
            };

            wfruntime.MakeTagNode(wfruntime.currentNodeId, tag);
            flowInstance.IsFinish = 4;//4表示驳回（需要申请者重新提交表单）
            if (resnode != "")
            {
                flowInstance.PreviousId = flowInstance.ActivityId;
                flowInstance.ActivityId = resnode;
                flowInstance.ActivityType = wfruntime.GetNodeType(resnode);
                flowInstance.ActivityName = wfruntime.nodes[resnode].name;
                flowInstance.MakerList = GetMakerList(wfruntime.nodes[resnode]);//当前节点可执行的人信息

                AddTransHistory(user, wfruntime);
            }

            UnitWork.Update(flowInstance);

            UnitWork.Add(new FlowInstanceOperationHistory
            {
                InstanceId = reqest.FlowInstanceId
                ,CreateUserId = user.Id
                ,CreateUserName = user.Name
                ,CreateDate = DateTime.Now
                ,Content = "【"
                          + wfruntime.currentNode.name
                          + "】【" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "】驳回,备注：" 
                          + reqest.VerificationOpinion

        });

            UnitWork.Save();

            return true;
        }
        #endregion

        /// <summary>
        /// 寻找该节点执行人
        /// </summary>
        /// <param name="wfruntime"></param>
        /// <returns></returns>
        private string GetMakerList(FlowRuntime wfruntime)
        {
            string makerList = "";
            if (wfruntime.nextNodeId == "-1")
            {
                throw (new Exception("无法寻找到下一个节点"));
            }
            if (wfruntime.nextNodeType == 0)//如果是会签节点
            {
                List<string> _nodelist = wfruntime.GetCountersigningNodeIdList(wfruntime.nextNodeId);
                string _makerList = "";
                foreach (string item in _nodelist)
                {
                    _makerList = GetMakerList(wfruntime.nodes[item]);
                    if (_makerList == "-1")
                    {
                        throw (new Exception("无法寻找到会签节点的审核者,请查看流程设计是否有问题!"));
                    }
                    if (_makerList == "1")
                    {
                        throw (new Exception("会签节点的审核者不能为所有人,请查看流程设计是否有问题!"));
                    }
                    if (makerList != "")
                    {
                        makerList += ",";
                    }
                    makerList += _makerList;
                }
            }
            else
            {
                makerList = GetMakerList(wfruntime.nextNode);
                if (makerList == "-1")
                {
                    throw (new Exception("无法寻找到节点的审核者,请查看流程设计是否有问题!"));
                }
            }

            return makerList;
        }
        /// <summary>
        /// 寻找该节点执行人
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private string GetMakerList(FlowNode node)
        {
            string makerList = "";

            if (node.setInfo == null)
            {
                makerList = "-1";
            }
            else
            {
                if (node.setInfo.NodeDesignate == Setinfo.ALL_USER)//所有成员
                {
                    makerList = "1";
                }
                else if (node.setInfo.NodeDesignate == Setinfo.SPECIAL_USER)//指定成员
                {
                    makerList = GenericHelpers.ArrayToString(node.setInfo.NodeDesignateData.users, makerList);

                    if (makerList == "")
                    {
                        makerList = "-1";
                    }
                }
                else if (node.setInfo.NodeDesignate == Setinfo.SPECIAL_ROLE)  //指定角色
                {
                    var users = _revelanceApp.Get(Define.USERROLE, false, node.setInfo.NodeDesignateData.roles);
                    makerList = GenericHelpers.ArrayToString(users, makerList);

                    if (makerList == "")
                    {
                        makerList = "-1";
                    }
                }
            }
            return makerList;
        }

        /// <summary>
        /// 审核流程
        /// <para>李玉宝于2017-01-20 15:44:45</para>
        /// </summary>
        public void Verification(VerificationReq request)
        {
            //驳回
            if (request.VerificationFinally == "3")
            {
                NodeReject(request);
            }
            else if (request.VerificationFinally == "2")//表示不同意
            {
                NodeVerification(request.FlowInstanceId, false, request.VerificationOpinion);
            }
            else if (request.VerificationFinally == "1")//表示同意
            {
                NodeVerification(request.FlowInstanceId, true, request.VerificationOpinion);
            }
        }

        public void Update(FlowInstance flowScheme)
        {
            Repository.Update(flowScheme);
        }

        public TableData Load(QueryFlowInstanceListReq request)
        {
            var result = new TableData();
            var user = _auth.GetCurrentUser();

            if (request.type == "wait")   //待办事项
            {
                result.count = UnitWork.Find<FlowInstance>(u => u.MakerList =="1" || u.MakerList.Contains(user.User.Id)).Count();

                result.data = UnitWork.Find<FlowInstance>(request.page, request.limit, "CreateDate descending", 
                    u => u.MakerList == "1" || u.MakerList.Contains(user.User.Id)).ToList();

            }
            else if (request.type == "disposed")  //已办事项（即我参与过的流程）
            {
                var instances = UnitWork.Find<FlowInstanceTransitionHistory>(u => u.CreateUserId == user.User.Id)
                    .Select(u => u.InstanceId).Distinct();
                var query = from ti in instances
                    join ct in UnitWork.Find<FlowInstance>(null) on ti equals ct.Id
                        into tmp
                    from ct in tmp.DefaultIfEmpty()
                    select ct;

                result.data = query.OrderByDescending(u => u.CreateDate)
                    .Skip((request.page - 1) * request.limit)
                    .Take(request.limit).ToList();
                result.count = instances.Count();
            }
            else  //我的流程
            {
                result.count = UnitWork.Find<FlowInstance>(u => u.CreateUserId == user.User.Id).Count();
                result.data = UnitWork.Find<FlowInstance>(request.page, request.limit,
                    "CreateDate descending", u => u.CreateUserId == user.User.Id).ToList();
            }

            return result;
        }

        /// <summary>
        /// 添加扭转记录
        /// </summary>
        private void AddTransHistory(User user, FlowRuntime wfruntime)
        {
            UnitWork.Add(new FlowInstanceTransitionHistory
            {
                InstanceId = wfruntime.flowInstanceId,
                CreateUserId = user.Id,
                CreateUserName = user.Name,
                FromNodeId = wfruntime.currentNodeId,
                FromNodeName = wfruntime.currentNode.name,
                FromNodeType = wfruntime.currentNodeType,
                ToNodeId = wfruntime.nextNodeId,
                ToNodeName = wfruntime.nextNode.name,
                ToNodeType = wfruntime.nextNodeType,
                IsFinish = wfruntime.nextNodeType == 4 ? 1 : 0,
                TransitionSate = 0
            });
        }

        public FlowInstanceApp(IUnitWork unitWork, IRepository<FlowInstance> repository
        ,IAuth auth ,RevelanceManagerApp app) : base(unitWork, repository)
        {
            _auth = auth;
            _revelanceApp = app;
        }
    }
}

