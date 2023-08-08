#region copyright
//------------------------------------------------------------------------------
//  �˴����Ȩ����Ϊȫ�ļ����ǣ�����ԭ�����ر������������·��ֶ�����
//  �˴����Ȩ�����ر�������Ĵ��룩�����߱���Diego����
//  Դ����ʹ��Э����ѭ���ֿ�Ŀ�ԴЭ�鼰����Э��
//  GiteeԴ����ֿ⣺https://gitee.com/diego2098/ThingsGateway
//  GithubԴ����ֿ⣺https://github.com/kimdiego2098/ThingsGateway
//  ʹ���ĵ���https://diego2098.gitee.io/thingsgateway-docs/
//  QQȺ��605534569
//------------------------------------------------------------------------------
#endregion

using Microsoft.AspNetCore.Components;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using ThingsGateway.Admin.Core;
using ThingsGateway.Application;
using ThingsGateway.Foundation.Adapter.OPCDA.Rcw;

using Yitter.IdGenerator;

namespace ThingsGateway.OPCDA;
/// <summary>
/// �������
/// </summary>
public partial class ImportVariable
{
    private List<BrowseElement> actived = new();

    private ItemProperty[] nodeAttributes;

    private List<OPCDATagModel> Nodes = new();

    private bool overlay = true;

    /// <summary>
    /// opc����
    /// </summary>
    [Parameter]
    public ThingsGateway.Foundation.Adapter.OPCDA.OPCDAClient PLC { get; set; }

    private List<BrowseElement> Actived
    {
        get => actived;
        set
        {
            if (actived?.FirstOrDefault() != value?.FirstOrDefault() && value?.Count > 0)
            {
                actived = value;
                nodeAttributes = actived.FirstOrDefault().Properties;
            }
        }
    }

    [Inject]
    IDriverPluginService DriverPluginService { get; set; }

    private List<BrowseElement> Selected { get; set; } = new();

    /// <summary>
    /// ��ȡ�豸������б�
    /// </summary>
    /// <returns></returns>
    public (CollectDevice, List<DeviceVariable>) GetImportVariableList()
    {
        var device = GetImportDevice();
        var data = Selected.Select(a =>
        {
            if (string.IsNullOrEmpty(a.ItemName))
            {
                return null;
            }

            ProtectTypeEnum level = ProtectTypeEnum.ReadOnly;
            try
            {
                var userAccessLevel = (accessRights)(a.Properties.FirstOrDefault(b => b.ID.Code == 5).Value);
                level = userAccessLevel == accessRights.readable ? userAccessLevel == accessRights.writable ? ProtectTypeEnum.WriteOnly : ProtectTypeEnum.ReadOnly : ProtectTypeEnum.ReadWrite;
            }
            catch
            {
            }

            var id = YitIdHelper.NextId();
            return new DeviceVariable()
            {
                Name = a.Name + "-" + id,
                VariableAddress = a.ItemName,
                DeviceId = device.Id,
                Id = id,
                ProtectTypeEnum = level,
                IntervalTime = 1000,
                RpcWriteEnable = true,
            };
        }).Where(a => a != null).ToList();
        return (device, data);
    }

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        await Task.Factory.StartNew(async () =>
        {
            Nodes = PopulateBranch("");
            overlay = false;
            await InvokeAsync(StateHasChanged);
        });
    }
    private CollectDevice GetImportDevice()
    {
        var id = YitIdHelper.NextId();
        var data = new CollectDevice()
        {
            Name = PLC.OPCNode.OPCName + "-" + id,
            Id = id,
            Enable = true,
            IsLogOut = true,
            DevicePropertys = new(),
            PluginId = DriverPluginService.GetIdByName(nameof(OPCDAClient)).ToLong(),
        };
        data.DevicePropertys.Add(new() { PropertyName = nameof(OPCDAClientProperty.OPCName), Value = PLC.OPCNode.OPCName, Description = typeof(OPCDAClientProperty).GetProperty(nameof(OPCDAClientProperty.OPCName)).GetCustomAttribute<DevicePropertyAttribute>().Description });
        data.DevicePropertys.Add(new() { PropertyName = nameof(OPCDAClientProperty.OPCIP), Value = PLC.OPCNode.OPCIP, Description = typeof(OPCDAClientProperty).GetProperty(nameof(OPCDAClientProperty.OPCIP)).GetCustomAttribute<DevicePropertyAttribute>().Description });
        data.DevicePropertys.Add(new() { PropertyName = nameof(OPCDAClientProperty.ActiveSubscribe), Value = PLC.OPCNode.ActiveSubscribe.ToString(), Description = typeof(OPCDAClientProperty).GetProperty(nameof(OPCDAClientProperty.ActiveSubscribe)).GetCustomAttribute<DevicePropertyAttribute>().Description });
        data.DevicePropertys.Add(new() { PropertyName = nameof(OPCDAClientProperty.CheckRate), Value = PLC.OPCNode.CheckRate.ToString(), Description = typeof(OPCDAClientProperty).GetProperty(nameof(OPCDAClientProperty.CheckRate)).GetCustomAttribute<DevicePropertyAttribute>().Description });
        data.DevicePropertys.Add(new() { PropertyName = nameof(OPCDAClientProperty.DeadBand), Value = PLC.OPCNode.DeadBand.ToString(), Description = typeof(OPCDAClientProperty).GetProperty(nameof(OPCDAClientProperty.DeadBand)).GetCustomAttribute<DevicePropertyAttribute>().Description });
        data.DevicePropertys.Add(new() { PropertyName = nameof(OPCDAClientProperty.GroupSize), Value = PLC.OPCNode.GroupSize.ToString(), Description = typeof(OPCDAClientProperty).GetProperty(nameof(OPCDAClientProperty.GroupSize)).GetCustomAttribute<DevicePropertyAttribute>().Description });
        data.DevicePropertys.Add(new() { PropertyName = nameof(OPCDAClientProperty.UpdateRate), Value = PLC.OPCNode.UpdateRate.ToString(), Description = typeof(OPCDAClientProperty).GetProperty(nameof(OPCDAClientProperty.UpdateRate)).GetCustomAttribute<DevicePropertyAttribute>().Description });
        return data;
    }

    private List<OPCDATagModel> PopulateBranch(string sourceId)
    {
        List<OPCDATagModel> nodes = new()
        {
            new OPCDATagModel() { Name = "Browsering..." }
        };
        var result = PLC.GetBrowseElements(sourceId);
        if (!result.IsSuccess)
        {
            return new()
            {
                new()
                {
                    Name = "δ�������",
                    Tag = new(),
                    Nodes = null
                }
            };
        }

        var references = result.Content;
        List<OPCDATagModel> list = new();
        if (references != null)
        {
            for (int ii = 0; ii < references.Count; ii++)
            {
                var target = references[ii];
                OPCDATagModel child = new()
                {
                    Name = target.Name,
                    Tag = target
                };
                if (target.HasChildren)
                {
                    child.Nodes = PopulateBranch(target.ItemName);
                }
                else
                {
                    child.Nodes = null;
                }

                list.Add(child);
            }
        }

        List<OPCDATagModel> listNode = list;
        nodes.Clear();
        nodes.AddRange(listNode.ToArray());
        return nodes;
    }

    private Task PopulateBranchAsync(OPCDATagModel model)
    {
        return Task.Run(() =>
        {
            var sourceId = model.Tag.ItemName;
            model.Nodes = PopulateBranch(sourceId);
        });
    }
    internal class OPCDATagModel
    {
        internal string Name { get; set; }
        internal string NodeId => (Tag?.ItemName)?.ToString();
        internal List<OPCDATagModel> Nodes { get; set; } = new();
        internal BrowseElement Tag { get; set; }
    }
}