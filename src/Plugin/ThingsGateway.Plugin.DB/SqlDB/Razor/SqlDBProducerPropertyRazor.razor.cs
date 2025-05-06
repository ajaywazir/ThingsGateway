// ------------------------------------------------------------------------------
// 此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
// 此代码版权（除特别声明外的代码）归作者本人Diego所有
// 源代码使用协议遵循本仓库的开源协议及附加协议
// Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
// Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
// 使用文档：https://thingsgateway.cn/
// QQ群：605534569
// ------------------------------------------------------------------------------

#pragma warning disable CA2007 // 考虑对等待的任务调用 ConfigureAwait
using BootstrapBlazor.Components;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using ThingsGateway.Gateway.Razor;
using ThingsGateway.Plugin.SqlDB;
using ThingsGateway.Razor;

namespace ThingsGateway.Debug
{
    public partial class SqlDBProducerPropertyRazor : IPropertyUIBase
    {
        [Inject]
        IStringLocalizer<ThingsGateway.Razor._Imports> RazorLocalizer { get; set; }


        [Parameter, EditorRequired]
        public IEnumerable<IEditorItem> PluginPropertyEditorItems { get; set; }
        [Parameter, EditorRequired]
        public string Id { get; set; }
        [Parameter, EditorRequired]
        public bool CanWrite { get; set; }
        [Parameter, EditorRequired]
        public ModelValueValidateForm Model { get; set; }

        IStringLocalizer SqlDBProducerPropertyLocalizer { get; set; }

        protected override Task OnParametersSetAsync()
        {
            SqlDBProducerPropertyLocalizer = App.CreateLocalizerByType(Model.Value.GetType());

            return base.OnParametersSetAsync();
        }

        private async Task CheckScript(SqlDBProducerProperty businessProperty, string pname)
        {
            IEnumerable<object> data = null;
            string script = null;
            {
                data = new List<VariableBasicData>() { new() {
                Name = "testName",
                DeviceName = "testDevice",
                Value = "1",
                ChangeTime = DateTime.Now,
                CollectTime = DateTime.Now,
                Remark1="1",
                Remark2="2",
                Remark3="3",
                Remark4="4",
                Remark5="5",
            } ,
             new() {
                Name = "testName2",
                DeviceName = "testDevice",
                Value = "1",
                ChangeTime = DateTime.Now,
                CollectTime = DateTime.Now,
                Remark1="1",
                Remark2="2",
                Remark3="3",
                Remark4="4",
                Remark5="5",
            } };
                script = pname == businessProperty.BigTextScriptHistoryTable ? businessProperty.BigTextScriptHistoryTable : businessProperty.BigTextScriptRealTable;

            }


            var op = new DialogOption()
            {
                IsScrolling = true,
                Title = RazorLocalizer["Check"],
                ShowFooter = false,
                ShowCloseButton = false,
                Size = Size.ExtraExtraLarge,
                FullScreenSize = FullScreenSize.None
            };

            op.Component = BootstrapDynamicComponent.CreateComponent<ScriptCheck>(new Dictionary<string, object?>
    {
        {nameof(ScriptCheck.Data),data },
        {nameof(ScriptCheck.Script),script },
        {nameof(ScriptCheck.OnGetDemo),()=>
                {
                    return
                    pname == nameof(SqlDBProducerProperty.BigTextScriptHistoryTable)?
                    """"
                    using ThingsGateway.Foundation;
                    
                    using System.Dynamic;
                    
                    using TouchSocket.Core;
                    public class S1 : DynamicSQLBase
                    {

                        public override async Task DBInit(ISqlSugarClient db, CancellationToken cancellationToken)
                        {

                            var sql = $"""

                                    """;
                            await db.Ado.ExecuteCommandAsync(sql, default, cancellationToken: cancellationToken).ConfigureAwait(false);
                        }
                        public override async Task DBInsertable(ISqlSugarClient db, IEnumerable<object> datas, CancellationToken cancellationToken)
                        {
                            var sql = $"""

                                    """;
                            await db.Ado.ExecuteCommandAsync(sql, default, cancellationToken: cancellationToken).ConfigureAwait(false);
                        }
                    }
                    
                    """"
                    :

                    pname == nameof(SqlDBProducerProperty.BigTextScriptRealTable)?

                    """"

                    using System.Dynamic;
                    using ThingsGateway.Foundation;
                    

                    using TouchSocket.Core;
                    public class S1 : DynamicSQLBase
                    {

                        public override async Task DBInit(ISqlSugarClient db, CancellationToken cancellationToken)
                        {

                            var sql = $"""

                                    """;
                            await db.Ado.ExecuteCommandAsync(sql, default, cancellationToken: cancellationToken).ConfigureAwait(false);
                        }
                        public override async Task DBInsertable(ISqlSugarClient db, IEnumerable<object> datas, CancellationToken cancellationToken)
                        {
                            var sql = $"""

                                    """;
                            await db.Ado.ExecuteCommandAsync(sql, default, cancellationToken: cancellationToken).ConfigureAwait(false);
                        }
                    }

                    """"
                    :
                    ""
                    ;
                }
            },
        {nameof(ScriptCheck.ScriptChanged),EventCallback.Factory.Create<string>(this, v =>
        {
                 if (pname == nameof(SqlDBProducerProperty.BigTextScriptHistoryTable))
    {
            businessProperty.BigTextScriptHistoryTable=v;

    }
    else if (pname == nameof(SqlDBProducerProperty.BigTextScriptRealTable))
    {
           businessProperty.BigTextScriptRealTable=v;
    }

        }) },

    });
            await DialogService.Show(op);

        }
        [Inject]
        DialogService DialogService { get; set; }
    }
}



