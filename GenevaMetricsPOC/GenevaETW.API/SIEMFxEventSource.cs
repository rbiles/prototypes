// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;
using System.Diagnostics.Tracing;

namespace GenevaETW.API
{
    public class SIEMfxEventSource : EventSource
    {
        public class Keywords
        {
            public const EventKeywords Diagnostic = (EventKeywords)1;
            public const EventKeywords Perf = (EventKeywords)2;
        }

        [Event(2, Message = "Application Unhandled Exception: {0}", Level = EventLevel.Error, Keywords = Keywords.Diagnostic)]
        public void UnhandledException(string message)
        {
            WriteEvent(2, message);
        }

        [Event(3, Message = "Application Warning: {0}", Level = EventLevel.Warning, Keywords = Keywords.Diagnostic)]
        public void Warning(string message)
        {
            WriteEvent(3, message);
        }

        [Event(4, Message = "Application Information {0}: Message: {1}", Level = EventLevel.Informational, Keywords = Keywords.Diagnostic)]
        public void Information(string infoType, string message)
        {
            WriteEvent(4, infoType, message);
        }


        [Event(3001, Message = "Application Failure: {0}", Level = EventLevel.Error, Keywords = Keywords.Diagnostic)]
        public void Failure(string message)
        {
            if (IsEnabled())
            {
                WriteEvent(3001, message);
            }
        }

        [Event(3002, Message = "Starting up service: {0}", Keywords = Keywords.Perf, Level = EventLevel.LogAlways)]
        public void ServiceStart(string serviceName)
        {
            if (IsEnabled())
            {
                WriteEvent(3002, serviceName);
            }
        }

        [Event(3003, Message = "Stopping service: {0}", Keywords = Keywords.Perf, Level = EventLevel.LogAlways)]
        public void ServiceStop(string serviceName)
        {
            if (IsEnabled())
            {
                WriteEvent(3003, serviceName);
            }
        }

        [Event(3004, Message = "Error getting Secret from KeyVault: {0} Exception: {1}", Keywords = Keywords.Diagnostic, Level = EventLevel.Error)]
        public void ExceptionGettingSecretFromKeyVault(string secretName, string exception)
        {
            if (IsEnabled())
            {
                WriteEvent(3004, secretName, exception);
            }
        }

        [Event(3005, Message = "Successfully Retrieved Cert from CertStore Thumbprint:{0}  StoreLocation: {1}", Keywords = Keywords.Perf, Level = EventLevel.LogAlways)]
        public void GotCertFromCertStore(string thumbprint, string storeLocation)
        {
            if (IsEnabled())
            {
                WriteEvent(3005, thumbprint, storeLocation);
            }
        }

        [Event(3006, Message = "Successfully Retrieved Cert from CertStore Thumbprint:{0} StoreLocation: {1}", Keywords = Keywords.Perf, Level = EventLevel.Error)]
        public void CertFromCertStoreFailed(string thumbprint, string storeLocation)
        {
            if (IsEnabled())
            {
                WriteEvent(3006, thumbprint, storeLocation);
            }
        }

        [Event(3007, Message = "Failed to Obtain KeyVault JWT Token", Keywords = Keywords.Perf, Level = EventLevel.Error)]
        public void FailedToObtainKeyVaultJWTToken()
        {
            if (IsEnabled())
            {
                WriteEvent(3007);
            }
        }

        [Event(3008, Message = "Failed to add node command to KqlNode. Query: {0}.", Keywords = Keywords.Perf, Level = EventLevel.Error)]
        public void FailedToAddNodeCommandToKqlNode(string query, string exception)
        {
            if (IsEnabled())
            {
                WriteEvent(3008, query, exception);
            }
        }

        [Event(3009, Message = "Failed to execute node command. Query: {0}.", Keywords = Keywords.Perf, Level = EventLevel.Error)]
        public void FailedToExecuteNodeCommand(string query, string failurePayload)
        {
            if (IsEnabled())
            {
                WriteEvent(3009, query, failurePayload);
            }
        }

        [Event(3010, Message = "Query: {0}.", Keywords = Keywords.Perf, Level = EventLevel.LogAlways)]
        public void QueryPerformance(string query, string payload)
        {
            if (IsEnabled())
            {
                WriteEvent(3010, query, payload);
            }
        }

        [Event(3011, Message = "Alert triggered. Destination:{0}.", Keywords = Keywords.Diagnostic, Level = EventLevel.Informational)]
        public void AlertTriggered(string destination, string payload)
        {
            if (IsEnabled())
            {
                WriteEvent(3011, destination, payload);
            }
        }

        [Event(3012, Message = "Alert processing error. Destination:{0}.", Keywords = Keywords.Diagnostic, Level = EventLevel.Error)]
        public void AlertProcessingError(string destination, string exception)
        {
            if (IsEnabled())
            {
                WriteEvent(3012, destination, exception);
            }
        }

        [Event(3013, Message = "Object disposed exception encounted. Exception: {0}", Keywords = Keywords.Diagnostic, Level = EventLevel.LogAlways)]
        public void ObjectDisposedException(string exception)
        {
            if (IsEnabled())
            {
                WriteEvent(3013, exception);
            }
        }

        [Event(3014, Message = "Alert dropped. Destination: {0}. Reason: {1}", Keywords = Keywords.Diagnostic, Level = EventLevel.Informational)]
        public void AlertDropped(string destination, string reason, string payload)
        {
            if (IsEnabled())
            {
                WriteEvent(3014, destination, reason, payload);
            }
        }

        [Event(3015, Message = "Alert transformed. Destination:{0}.", Keywords = Keywords.Diagnostic, Level = EventLevel.Verbose)]
        public void AlertTransformed(string destination, string payload)
        {
            if (IsEnabled())
            {
                WriteEvent(3015, destination, payload);
            }
        }

        [Event(3016, Message = "Alert transformation failed. Destination: {0}. Reason: {1}.", Keywords = Keywords.Diagnostic, Level = EventLevel.Error)]
        public void AlertTransformationFailed(string destination, string reason, string payload)
        {
            if (IsEnabled())
            {
                WriteEvent(3016, destination, reason, payload);
            }
        }

        [Event(3017, Message = "Alert transformer returned null. Destination: {0}.", Keywords = Keywords.Diagnostic, Level = EventLevel.Informational)]
        public void AlertTransformerNull(string destination, string payload)
        {
            if (IsEnabled())
            {
                WriteEvent(3017, destination, payload);
            }
        }

        [Event(3018, Message = "Error getting Certificate from KeyVault: {0} Exception: {1}", Keywords = Keywords.Diagnostic, Level = EventLevel.Error)]
        public void ExceptionGettingCertificateFromKeyVault(string certificateName, string payload)
        {
            if (IsEnabled())
            {
                WriteEvent(3018, certificateName, payload);
            }
        }

        [Event(3019, Message = "Vault <{0}> not found.", Keywords = Keywords.Diagnostic, Level = EventLevel.Error)]
        public void VaultNotFound(string vaultName)
        {
            if (IsEnabled())
            {
                WriteEvent(3019, vaultName);
            }
        }


        [Event(9101, Message = "{0}", Keywords = Keywords.Diagnostic, Level = EventLevel.Informational)]
        public void HttpFileReceived(string message, string clientData = null)
        {
            if (IsEnabled())
            {
                WriteEvent(9101, message, clientData);
            }
        }

        [Event(9102, Message = "HttpReceiver: On Timer for File Receiver Exception: {0}", Keywords = Keywords.Perf, Level = EventLevel.Error)]
        public void OnFileUploaderError(string exception)
        {
            if (IsEnabled())
            {
                WriteEvent(9102, exception);
            }
        }

        [Event(9103, Message = "{0}", Keywords = Keywords.Diagnostic, Level = EventLevel.Informational)]
        public void HttpFileRequestCouldNoBeProcessed(string message, string clientData = null)
        {
            if (IsEnabled())
            {
                WriteEvent(9103, message, clientData);
            }
        }

        [Event(9104, Message = "{0}", Keywords = Keywords.Diagnostic, Level = EventLevel.Informational)]
        public void HttpReceiverHeartbeat(string message, string clientData = null)
        {
            if (IsEnabled())
            {
                WriteEvent(9104, message, clientData);
            }
        }

        [Event(9105, Message = "{0}", Keywords = Keywords.Diagnostic, Level = EventLevel.Error)]
        public void HttpFileReceiverFatalError(string message)
        {
            if (IsEnabled())
            {
                WriteEvent(9105, message);
            }
        }

        [Event(9106, Message = "{0}", Keywords = Keywords.Diagnostic, Level = EventLevel.Error)]
        public void AppSettingsFileNotFound(string message)
        {
            if (IsEnabled())
            {
                WriteEvent(9106, message);
            }
        }

        [Event(9107, Message = "{0}", Keywords = Keywords.Diagnostic, Level = EventLevel.Informational)]
        public void AddJsonFileToConfig(string message)
        {
            if (IsEnabled())
            {
                WriteEvent(9107, message);
            }
        }

        [Event(9108, Message = "{0}", Keywords = Keywords.Diagnostic, Level = EventLevel.Error)]
        public void JsonConfigFileNotFound(string message)
        {
            if (IsEnabled())
            {
                WriteEvent(9108, message);
            }
        }

        [Event(9109, Message = "{0}", Keywords = Keywords.Diagnostic, Level = EventLevel.Error)]
        public void DefsStorageManagerInitializeException(string message)
        {
            if (IsEnabled())
            {
                WriteEvent(9109, message);
            }
        }

        [Event(9110, Message = "Removed uploader from EventSinkManager. NodeCommand name: {0}, Uploader identifier: {1}.", Keywords = Keywords.Diagnostic, Level = EventLevel.LogAlways, Opcode = EventOpcode.Send)]
        public void RemovedUploaderFromEventSinkManager(Guid relatedActivityId, string nodeCommandName, string uploaderIdentifier)
        {
            if (IsEnabled())
            {
                WriteEventWithRelatedActivityId(9110, relatedActivityId, nodeCommandName, uploaderIdentifier);
            }
        }

        [Event(9111, Message = "Uploader created. NodeCommand: {0}. EventSinkIdentifier: {1}", Keywords = Keywords.Diagnostic, Level = EventLevel.LogAlways, Opcode = EventOpcode.Send)]
        public void NewUploaderCreated(Guid relatedActivityId, string nodeCommand, string eventSinkIdentifier)
        {
            if (IsEnabled())
            {
                WriteEventWithRelatedActivityId(9111, relatedActivityId, nodeCommand, eventSinkIdentifier);
            }
        }

        [Event(9112, Message = "Reusing existing uploader. NodeCommand: {0}. EventSinkIdentifier: {1}", Keywords = Keywords.Diagnostic, Level = EventLevel.LogAlways, Opcode = EventOpcode.Send)]
        public void UsingExistingUploader(Guid relatedActivityId, string nodeCommand, string eventSinkIdentifier)
        {
            if (IsEnabled())
            {
                WriteEventWithRelatedActivityId(9112, relatedActivityId, nodeCommand, eventSinkIdentifier);
            }
        }

        [Event(9113, Message = "Uploader completed and shutdown. NodeCommand: {0}. EventSinkIdentifier: {1}", Keywords = Keywords.Diagnostic, Level = EventLevel.LogAlways, Opcode = EventOpcode.Send)]
        public void UploaderCompletedShutdown(Guid relatedActivityId, string nodeCommand, string eventSinkIdentifier)
        {
            if (IsEnabled())
            {
                WriteEventWithRelatedActivityId(9113, relatedActivityId, nodeCommand, eventSinkIdentifier);
            }
        }

        [Event(9114, Message = "Removed NodeCommand from EventSinkManager since it has no uploaders. NodeCommand name: {0}.", Keywords = Keywords.Diagnostic, Level = EventLevel.LogAlways, Opcode = EventOpcode.Send)]
        public void RemovedNodeCommandFromEventSinkManager(Guid relatedActivityId, string nodeCommandName)
        {
            if (IsEnabled())
            {
                WriteEventWithRelatedActivityId(9114, relatedActivityId, nodeCommandName);
            }
        }

        [Event(9115, Message = "EventSinkManager shutting down.", Keywords = Keywords.Diagnostic, Level = EventLevel.LogAlways, Opcode = EventOpcode.Send)]
        public void EventSinkManagerShuttingDown()
        {
            if (IsEnabled())
            {
                WriteEvent(9115);
            }
        }

        [Event(9116, Message = "EventSinkManager called OnComplete on all uploaders.", Keywords = Keywords.Diagnostic, Level = EventLevel.LogAlways, Opcode = EventOpcode.Send)]
        public void EventSinkManagerComplete()
        {
            if (IsEnabled())
            {
                WriteEvent(9116);
            }
        }

        [Event(9117, Message = "Destination: {0}. Configuration error: {0}", Keywords = Keywords.Diagnostic, Level = EventLevel.Error, Opcode = EventOpcode.Send)]
        public void ConfigurationError(string destination, string message, string payload)
        {
            if (IsEnabled())
            {
                WriteEvent(9117, destination, message, payload);
            }
        }

        [Event(7001, Message = "IfxAuditInitialiizedStatus {0}", Level = EventLevel.Informational, Keywords = Keywords.Diagnostic)]
        public void IfxAuditInitialiizedStatus(string message)
        {
            if (IsEnabled())
            {
                WriteEvent(7001, message);
            }
        }

        [Event(7002, Message = "IfxAuditMessageStatus {0}", Level = EventLevel.Informational, Keywords = Keywords.Diagnostic)]
        public void IfxAuditMessageStatus(string message)
        {
            if (IsEnabled())
            {
                WriteEvent(7002, message);
            }
        }

        [Event(7003, Message = "IfxAuditErrorMessage {0}", Level = EventLevel.Error, Keywords = Keywords.Diagnostic)]
        public void IfxAuditErrorMessage(string message)
        {
            if (IsEnabled())
            {
                WriteEvent(7003, message);
            }
        }

        
        public static SIEMfxEventSource Log = new SIEMfxEventSource();
    }
}
