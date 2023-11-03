using Database.Common;
using Database.Extensions;
using Parus.Database.Specialized;
using System;
using System.Data;
using System.IO;
using System.Text;
using System.Web.Mvc;
using System.Xml;

namespace P8PanelsParusOnlineExt.Controllers
{
    public class P8PanelsController : Controller
    {
        private readonly IContextualParusDatabaseFactoryProvider _databaseProvider;
        private readonly static string _STATUS_ERR = "ERR";
        private readonly static string _STATUS_OK = "OK";

        public P8PanelsController(IContextualParusDatabaseFactoryProvider databaseProvider)
        {
            _databaseProvider = databaseProvider;
        }

        private string GetRequestContentAsString()
        {
            using (var receiveStream = Request.InputStream)
            {
                using (var readStream = new StreamReader(receiveStream, Encoding.UTF8))
                {
                    return readStream.ReadToEnd();
                }
            }
        }

        //Формирование типового ответа
        private XmlDocument MakeRespond(string status, string message = null, string payload = null)
        {
            if (status == null || (status != _STATUS_OK && status != _STATUS_ERR)) return MakeErrorRespond("Неопределённое состояние ответа");
            if (status == _STATUS_ERR && (message == null || message == "")) return MakeErrorRespond("Неопределённое сообщение об ошибке");
            //Заголовок документа
            XmlDocument doc = new XmlDocument();
            XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlElement root = doc.DocumentElement;
            doc.InsertBefore(xmlDeclaration, root);
            //Корень ответа - XRESPOND
            XmlElement respondNode = doc.CreateElement(string.Empty, "XRESPOND", string.Empty);
            doc.AppendChild(respondNode);
            //Статус ответа - SSTATUS
            XmlElement statusNode = doc.CreateElement(string.Empty, "SSTATUS", string.Empty);
            XmlText statusValue = doc.CreateTextNode(status);
            statusNode.AppendChild(statusValue);
            respondNode.AppendChild(statusNode);
            //Сообщение об ошибке - SMESSAGE
            if (status == _STATUS_ERR)
            {
                XmlElement messageNode = doc.CreateElement(string.Empty, "SMESSAGE", string.Empty);
                XmlText messageValue = doc.CreateTextNode(message);
                messageNode.AppendChild(messageValue);
                respondNode.AppendChild(messageNode);
            }
            //Данные ответа - XPAYLOAD
            if (status == _STATUS_OK)
            {
                XmlElement payloadNode = doc.CreateElement(string.Empty, "XPAYLOAD", string.Empty);
                if (payload != null)
                {
                    try
                    {
                        XmlDocument payloadDoc = new XmlDocument();
                        payloadDoc.LoadXml(payload);
                        foreach (XmlNode payloadDocNode in payloadDoc.DocumentElement.ChildNodes)
                        {
                            XmlNode imported = doc.ImportNode(payloadDocNode, true);
                            payloadNode.AppendChild(imported);
                        }
                    }
                    catch
                    {
                        XmlText payloadValue = doc.CreateTextNode(payload);
                        payloadNode.AppendChild(payloadValue);
                    }
                }
                else
                {
                    XmlText payloadValue = doc.CreateTextNode("");
                    payloadNode.AppendChild(payloadValue);
                }
                respondNode.AppendChild(payloadNode);
            }
            //Вернём собранный ответ
            return doc;
        }

        //Формирование ответа с ошибкой
        private XmlDocument MakeErrorRespond(string message)
        {
            return MakeRespond(status: _STATUS_ERR, message: message);
        }

        //Формирование ответа с данными
        private XmlDocument MakeOkRespond(string payload)
        {
            return MakeRespond(status: _STATUS_OK, payload: payload);
        }

        [HttpPost]
        public ActionResult Process()
        {
            try
            {
                string requestData = GetRequestContentAsString();
                string dbData;
                var parusDatabaseFactory = _databaseProvider.GetDatabaseFactory();
                using (var connection = parusDatabaseFactory.CreateConnection())
                {
                    using (var command = parusDatabaseFactory.CreateProcedure(connection, "PKG_P8PANELS.PROCESS"))
                    {
                        command.Parameters.Add("CIN", CommonDbType.Clob, requestData, ParameterDirection.Input);
                        command.Parameters.Add("COUT", CommonDbType.Clob, ParameterDirection.Output);
                        command.ExecuteNonQuery();
                        dbData = command.Parameters.FromDb<string>("COUT");
                    }
                }
                return this.Content(MakeOkRespond(dbData).OuterXml, "application/xml");
            }
            catch (Exception e)
            {
                return this.Content(MakeErrorRespond(e.Message).OuterXml, "application/xml");
            }
        }

        [HttpPost]
        public ActionResult GetConfig()
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(Module.configFile);
                return this.Content(MakeOkRespond(doc.OuterXml).OuterXml, "application/xml");
            }
            catch (Exception e)
            {
                return this.Content(MakeErrorRespond(e.Message).OuterXml, "application/xml");
            }
        }
    }
}