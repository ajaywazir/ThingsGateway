﻿namespace ThingsGateway.Siemens
{
    public class S7_300 : S7
    {
        public S7_300(IServiceScopeFactory scopeFactory) : base(scopeFactory)
        {
        }

        protected override void Init(CollectDeviceRunTime device, object client = null)
        {
            if (client == null)
            {
                TouchSocketConfig.SetRemoteIPHost(new IPHost(IPAddress.Parse(IP), Port))
                    .SetBufferLength(1024);
                client = TouchSocketConfig.Container.Resolve<TcpClient>();
                ((TcpClient)client).Setup(TouchSocketConfig);
            }
            //载入配置
            _plc = new((TcpClient)client, SiemensEnum.S300);
            _plc.DataFormat = DataFormat;
            _plc.ConnectTimeOut = ConnectTimeOut;
            _plc.TimeOut = TimeOut;

        }


    }
}