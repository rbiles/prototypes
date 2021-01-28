using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIEMfx.SentinelWorkspacePoc
{
    public class SyslogToCef
    {
        private Dictionary<string, string> cefSeverityDictionary;

        private Dictionary<string, string> cefExtensionDictionary;

        public SyslogToCef()
        {
            // constructor
            InitializeCefSeverityDictionary();

            InitializeCefExtensionDictionary();
        }

        public Dictionary<string, object> ConvertSyslogToCef(Dictionary<string, object> syslogRecord)
        {
            Dictionary<string, object> cefReturnDictionary = new Dictionary<string, object>();

            try
            {
                StringBuilder cefExtensionValues = new StringBuilder();

                // CEF example: "CEF:0|Microsoft|ATA|1.9.0.0|GatewayStartFailureMonitoringAlert|GatewayStartFailureMonitoringAlert|5|externalId=1018 cs1Label=url"
                // CEF header definitions: CEF:Version|Device Vendor|Device Product|Device Version|Device Event Class ID|Name|Severity|[Extension]
                var cefVersion = "0"; // CEF version 0 is current
                var cefDeviceVendor = "Unknown"; //"junos" in raw message, not extracted
                var cefDeviceProduct = "Unknown";
                var cefDeviceVersion = "Unknown"; //version in raw message, not extracted

                // CEF header DeviceEventClassId maps to AppName
                if (!syslogRecord.TryGetValue("AppName", out object cefDeviceEventClassId))
                {
                    cefDeviceEventClassId = "Unknown";
                }

                // CEF header Name maps to MsgId
                if (!syslogRecord.TryGetValue("MsgId", out object cefName))
                {
                    cefName = "Unknown";
                }

                // CEF header Severity maps to Severity
                if (!syslogRecord.TryGetValue("Severity", out object cefSeverity))
                {
                    cefSeverity = "Unknown";
                }

                // convert Severity to numeric value
                if (!cefSeverityDictionary.TryGetValue(cefSeverity.ToString().ToLower(), out string cefSeverityInt))
                {
                    cefSeverityInt = "0";
                }

                // CEF extension key pairs map to ExtractedData
                if (!syslogRecord.TryGetValue("ExtractedData", out object syslogRecordExtractedData))
                {
                    syslogRecordExtractedData = null;
                }

                var extractedData = JsonConvert.DeserializeObject<Dictionary<string, object>>(syslogRecordExtractedData.ToString());

                foreach (var item in cefExtensionDictionary)
                {
                    if (!extractedData.TryGetValue(item.Key, out object CefExtensionValue))
                    {
                        CefExtensionValue = null;
                    }
                    else
                    {
                        cefExtensionValues.Append($"{item.Value}={CefExtensionValue} ");
                    }
                }

                // add original payload to CEF as deviceCustomString1
                if (!syslogRecord.TryGetValue("Payload", out object syslogv2Payload))
                {
                    syslogv2Payload = "";
                }

                // string deviceCustomString1 = "\"" + " cs1=" +  syslogv2Payload.ToString() + "\""; // all text after first equal sign gets parsed to AdditonalExtensions

                // clean up payload so included keypairs are not parsed as AdditionalExtensions
                string deviceCustomString1 = " cs1=" + syslogv2Payload.ToString().Replace("=", "\\=");

                // build CEF message
                cefReturnDictionary.Add("Message", "CEF:" + cefVersion + "|" + cefDeviceVendor + "|" + cefDeviceProduct + "|" + cefDeviceVersion + "|" + cefDeviceEventClassId + "|" + cefName + "|" + cefSeverityInt + "|" + cefExtensionValues.ToString() + deviceCustomString1);

                // overwrite severity as numeric value
                cefReturnDictionary.Add("Severity", cefSeverityInt.ToString());

                return cefReturnDictionary;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return new Dictionary<string, object>();
            }
        }

        private void InitializeCefSeverityDictionary()
        {
            cefSeverityDictionary = new Dictionary<string, string>();
            cefSeverityDictionary.Add("unknown", "0");
            cefSeverityDictionary.Add("informational", "1");
            cefSeverityDictionary.Add("warning", "3");
            cefSeverityDictionary.Add("minor", "6");
            cefSeverityDictionary.Add("major", "8");
            cefSeverityDictionary.Add("critical", "9");
            cefSeverityDictionary.Add("fatal", "10");
        }

        private void InitializeCefExtensionDictionary()
        {
            cefExtensionDictionary = new Dictionary<string, string>();
            // key = syslog extracted value, value = cef short name
            cefExtensionDictionary.Add("nat-destination-address", "destinationTranslatedAddress");
            cefExtensionDictionary.Add("nat-destination-port", "destinationTranslatedPort");
            cefExtensionDictionary.Add("device_id", "deviceExternalId");
            cefExtensionDictionary.Add("devid", "deviceExternalId");
            cefExtensionDictionary.Add("dstserver", "dhost");
            cefExtensionDictionary.Add("dst_mac", "dmac");
            cefExtensionDictionary.Add("dstmac", "dmac");
            cefExtensionDictionary.Add("destination-port", "dpt");
            cefExtensionDictionary.Add("dst_port", "dpt");
            cefExtensionDictionary.Add("dstport", "dpt");
            cefExtensionDictionary.Add("tran_dst_port", "dpt");
            cefExtensionDictionary.Add("destination-address", "dst");
            cefExtensionDictionary.Add("dst_ip", "dst");
            cefExtensionDictionary.Add("dstip", "dst");
            cefExtensionDictionary.Add("tran_dst_ip", "dst");
            cefExtensionDictionary.Add("pid", "dvcpid");
            cefExtensionDictionary.Add("device_name", "dvchost");
            cefExtensionDictionary.Add("devname", "dvchost");
            cefExtensionDictionary.Add("hostname", "dvchost");
            cefExtensionDictionary.Add("inbound-bytes", "in");
            cefExtensionDictionary.Add("lanin", "in");
            cefExtensionDictionary.Add("rcvdbyte", "in");
            cefExtensionDictionary.Add("recv_bytes", "in");
            cefExtensionDictionary.Add("message", "msg");
            cefExtensionDictionary.Add("msg", "msg");
            cefExtensionDictionary.Add("lanout", "out");
            cefExtensionDictionary.Add("outbound-bytes", "out");
            cefExtensionDictionary.Add("sent_bytes", "out");
            cefExtensionDictionary.Add("sentbyte", "out");
            cefExtensionDictionary.Add("proto", "proto");
            cefExtensionDictionary.Add("protocol", "proto");
            cefExtensionDictionary.Add("protocol-id", "proto");
            cefExtensionDictionary.Add("protocol-name", "proto");
            cefExtensionDictionary.Add("reason", "reason"); // not defined as CEF by Sentinel, gets applied to AdditionalExtensions
            cefExtensionDictionary.Add("nat-source-address", "sourceTranslatedAddress");
            cefExtensionDictionary.Add("nat-source-port", "sourceTranslatedPort");
            cefExtensionDictionary.Add("srchost", "shost");
            cefExtensionDictionary.Add("src_mac", "smac");
            cefExtensionDictionary.Add("srcmac", "smac");
            cefExtensionDictionary.Add("source-port", "spt");
            cefExtensionDictionary.Add("src_port", "spt");
            cefExtensionDictionary.Add("srcport", "spt");
            cefExtensionDictionary.Add("tran_src_port", "spt");
            cefExtensionDictionary.Add("source-address", "src");
            cefExtensionDictionary.Add("src_ip", "src");
            cefExtensionDictionary.Add("srcip", "src");
            cefExtensionDictionary.Add("tran_src_ip", "src");
        }
    }
}
