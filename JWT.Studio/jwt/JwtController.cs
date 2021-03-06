﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using jwt.internals;
using System.Reflection;
using jwt.CodeGen;
using System.Text.RegularExpressions;
namespace Jwt.Controller
{
    public class JwtController : BaseController
    {
        public void Index()
        {
            string nameSpace = GetType().Assembly.GetName().Name;
            string index = "jwt.JwtResources.index.html";
            Response.ContentType = "text/html";
            using (Stream stream = GetType().Assembly.GetManifestResourceStream(index))
            {
                stream.CopyTo(Response.OutputStream);
            }

        }
        public void Resources(string file)
        {
            string ext = file.Substring(file.LastIndexOf(".") + 1);

            using (Stream stream = GetType().Assembly.GetManifestResourceStream("jwt.JwtResources." + file))
            {
                switch (ext)
                {
                    case "html":
                        Response.ContentType = "text/html";
                        break;
                    case "css":
                        Response.ContentType = "text/css";
                        break;
                    case "js":
                        Response.ContentType = "text/javascript";
                        break;
                }
                stream.CopyTo(Response.OutputStream);
            }

        }

        public JsonResult AddState(State state)
        {
            StateManager sm = new StateManager();
            sm.RootPath = Config.Root;
            sm.Deserialize();
            string res = sm.AddState(state);
            if (res != "Already Exist")
            {
                string templates = Config.Root + "Templates";
                if (!Directory.Exists(templates))
                {
                    Directory.CreateDirectory(templates);
                }
                string component = Config.Root + "Templates\\Layouts";
                if (!Directory.Exists(component))
                {
                    Directory.CreateDirectory(component);
                }
                if (!string.IsNullOrEmpty(state.TemplateUrl) && !System.IO.File.Exists(component + "\\" + state.TemplateUrl) && state.IsAbstract)
                {
                    System.IO.File.WriteAllText(component + "\\" + state.TemplateUrl, DateTime.Now.ToLongDateString());
                }
            }
            sm.Serialize();
            return Json(new { msg = res });
        }
        public JsonResult Remove(State state)
        {
            StateManager sm = new StateManager();
            sm.RootPath = Config.Root;
            sm.Deserialize();
            string res = sm.RemoveState(state);
            sm.Serialize();
            return Json(new { msg = res });
        }
        public JsonResult Update(State state)
        {
            StateManager sm = new StateManager();
            sm.RootPath = Config.Root;
            sm.Deserialize();
            string res = sm.UpdateState(state);
            sm.Serialize();
            return Json(new { msg = res });
        }
        public JsonResult GetAllState()
        {
            StateManager sm = new StateManager();
            sm.RootPath = Config.Root;
            sm.Deserialize();
            if (sm.StateList == null)
            {
                return Json(new { layout = new List<State>(), state = new List<State>() }, JsonRequestBehavior.AllowGet);
            }
            return Json(new { layout = sm.StateList.Where(x => x.IsAbstract) ?? new List<State>(), state = sm.StateList.Where(x => !x.IsAbstract) ?? new List<State>() }, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GenerateConfig()
        {
            try
            {
                StateManager sm = new StateManager();
                sm.RootPath = Config.Root;
                sm.Deserialize();
                StringBuilder strBuilder = new StringBuilder();
                StringBuilder jwt = new StringBuilder();
                jwt.Append("jwt._arr={"); bool isFirst = true;
                foreach (var item in sm.StateList)
                {
                    strBuilder.AppendLine();
                    if (item.IsAbstract)
                    {
                        strBuilder.AppendFormat("stateprovider.state('{0}',{1}abstract:true, url:'{2}',templateUrl: root + '{3}'", GetStateName(item, sm.StateList), "{", item.Url, "Templates/Layouts/" + item.TemplateUrl);
                    }
                    else
                    {
                        var stateName=GetStateName(item, sm.StateList);
                        jwt.AppendFormat((isFirst?"":",")+"'{0}':['{1}','']", item.StateName, stateName);
                        strBuilder.AppendFormat("stateprovider.state('{0}',{1}url:'{2}'", stateName, "{", item.Url);
                        if (!string.IsNullOrEmpty(item.TemplateUrl))
                        {
                            strBuilder.AppendFormat(",templateUrl:root + 'Templates/Components/{0}'", item.TemplateUrl);
                        }
                        isFirst = false;
                    }                   

                    if (!string.IsNullOrEmpty(item.StateController))
                    {
                        strBuilder.AppendFormat(",controller:'{0}'", item.StateController);
                    }

                    if (item.StateViews != null && item.StateViews.Count > 0)
                    {
                        isFirst = true;
                        string comma = "";
                        strBuilder.Append(",views:{");
                        foreach (StateView item2 in item.StateViews)
                        {
                            if (!isFirst) { comma = ","; }
                            strBuilder.AppendFormat("{0}'{1}':{2}controller:'{3}', templateUrl:root + 'Templates/Components/{4}'{5}", comma, item2.ViewName, "{", item2.ControllerName, item2.TemplateUrl, "}", comma);
                            isFirst = false;
                        }
                        strBuilder.Append("}");
                    }
                    strBuilder.Append("});");
                }
                jwt.Append("};");
                string router = Config.Root + "Scripts\\Router";
                if (!Directory.Exists(router))
                {
                    Directory.CreateDirectory(router);
                }
                string fLine = "angular.module('app').config(['$stateProvider', '$urlRouterProvider', function (stateprovider, routeProvider) {" + Environment.NewLine;
                fLine += "var root = '';";
                string lLine = Environment.NewLine + "}]);" + Environment.NewLine;
                System.IO.File.WriteAllText(router + "\\AppRouter.js", string.Format("{0}{1}{2}{3}", fLine, strBuilder, lLine, Environment.NewLine+jwt.ToString()));

            }
            catch (Exception ex)
            {

                return Json(new { msg = ex.Message }, JsonRequestBehavior.AllowGet);
            }
            return Json(new { msg = "Successfully generated." }, JsonRequestBehavior.AllowGet);
        }

        private string GetStateName(State state, List<State> list, string res = "")
        {
            if (string.IsNullOrEmpty(res))
            {
                res = state.StateName;
            }
            else
            {
                res = state.StateName + "." + res;
            }

            if (!string.IsNullOrEmpty(state.Parent) && state.Parent != "--Select--")
            {
                res = GetStateName(list.Find(x => x.StateName == state.Parent), list, res);
            }
            return res;
        }

        #region Code Gen

        public JsonResult GetTemplateList()
        {
            JSONData res = new JSONData();
            res.success = true;
            res.data = GetTemplateList(Config.Root);
            return Json(res, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetEntityList()
        {

            JSONData res = new JSONData();
            res.success = false;
            string entityPath = this.Config.EntityProject;
            if (entityPath == "101")
            {
                res.message = "Please provide entity project name into appSettings of web.config file";

                return Json(res, JsonRequestBehavior.AllowGet);
            }
            if (Directory.Exists(entityPath))
            {
                res.data = GetEntityList(entityPath);
                if (res.data == null)
                {
                    res.message = "Could not find the 'Entities' folder in your given entity project name";
                }
                else
                {
                    res.success = true;
                }
            }
            else
            {
                res.success = false;
                res.message = string.Format("'{0}' path is invalid", entityPath);
            }

            return Json(res, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetProperties(string entityName)
        {
            JSONData res = new JSONData();
            res.success = false;
            List<JPropertyInfo> list = new List<JPropertyInfo>();
            List<string> entityList = GetEntityList(Config.EntityProject);
            try
            {

                Assembly assembly = Assembly.Load(Config.EntityModule);
                Type xtype = assembly.GetType(Config.EntityModule + ".Entities." + entityName);
                System.Reflection.PropertyInfo[] propertyList = xtype.GetProperties();

                foreach (System.Reflection.PropertyInfo item in propertyList)
                {
                    JPropertyInfo prop = new JPropertyInfo();
                    string type = item.PropertyType.ToString();
                    if (type.Contains("Collection")) { continue; }
                    prop.PropertyName = item.Name;
                    prop.Xtype = type;
                    prop.HasDetail = !string.IsNullOrEmpty(entityList.FirstOrDefault(x => type.Contains(x)));
                    if (prop.HasDetail)
                    {
                        prop.Details = GetSubProperties(item.Name, assembly, type);
                    }

                    list.Add(prop);
                }
                res.success = true;

                res.data =SyncWithSavedWidget(list, entityName);
            }
            catch (Exception ex)
            {
                res.message = ex.Message;
            }
            return Json(res, JsonRequestBehavior.AllowGet);
        }

        private List<JPropertyInfo> SyncWithSavedWidget(List<JPropertyInfo> list, string widgetName)
        {
            WidgetManager widgetManager = new WidgetManager();
            widgetManager.RootPath = Config.Root;
            JwtWidget temp = widgetManager.GetWidgetByName(widgetName);
            if (temp == null) { return list; }
            foreach (var item in temp.PropertyList)
            {
                JPropertyInfo prop = list.Find(p => p.PropertyName == item.PropertyName);
                if (prop != null)
                {
                    list.Remove(prop);
                    list.Add(item);
                }
            }

            return list;

        }

        

        public JsonResult CodeGenerate(string entity, List<JPropertyInfo> props)
        {
            try
            {
                string templates = Config.Root + "Templates";
                if (!Directory.Exists(templates))
                {
                    Directory.CreateDirectory(templates);
                }
                string component = Config.Root + "Templates\\Components";
                if (!Directory.Exists(component))
                {
                    Directory.CreateDirectory(component);
                }

                //Generate scripts directories
                string script = Config.ScriptProject + "\\Scripts";
                if (!Directory.Exists(script))
                {
                    Directory.CreateDirectory(script);
                }
                script = Config.ScriptProject + "\\Scripts\\Controllers";
                if (!Directory.Exists(script))
                {
                    Directory.CreateDirectory(script);
                }
                script = Config.ScriptProject + "\\Scripts\\Services";
                if (!Directory.Exists(script))
                {
                    Directory.CreateDirectory(script);
                }
                //services
                script = Config.ServiceProject + "\\Interfaces";
                if (!Directory.Exists(script))
                {
                    Directory.CreateDirectory(script);
                }
                script = Config.ServiceProject + "\\Implementation";
                if (!Directory.Exists(script))
                {
                    Directory.CreateDirectory(script);
                }

                ICode code = new TemplateCode();
                System.IO.File.WriteAllText(component + "\\" + entity + ".html", code.CodeGenerate(entity, props));
                code = new JSController();
                code.Config = this.Config;
                System.IO.File.WriteAllText(Config.ScriptProject + "\\Scripts\\Controllers\\" + entity + "Ctrl.cs", code.CodeGenerate(entity, props));
                code = new JSService();
                code.Config = this.Config;
                System.IO.File.WriteAllText(Config.ScriptProject + "\\Scripts\\Services\\" + entity + "Service.cs", code.CodeGenerate(entity, props));
                code = new CSController();
                System.IO.File.WriteAllText(Config.Root + "Controllers\\" + entity + "Controller.cs", code.CodeGenerate(entity, props));
                code = new CSServiceInterface();
                System.IO.File.WriteAllText(Config.ServiceProject + "\\Interfaces\\I" + entity + "Service.cs", code.CodeGenerate(entity, props));
                code = new CSServiceImplementation();
                System.IO.File.WriteAllText(Config.ServiceProject + "\\Implementation\\" + entity + "Service.cs", code.CodeGenerate(entity, props));

                WidgetManager widgetManager = new WidgetManager();
                widgetManager.RootPath = Config.Root;
                widgetManager.AddWidget(new JwtWidget { Name = entity, PropertyList = props });
                return Json(new { message = "Successfully Generated." });
            }
            catch (Exception ex)
            {
                return Json(new { message = ex.ToString() });
            }
        }
        
        private List<JPropertyInfo> GetSubProperties(string entityName, Assembly assembly, string type)
        {
            List<JPropertyInfo> list = new List<JPropertyInfo>();

            Type xtype = assembly.GetType(Config.EntityModule + ".Entities." + entityName);
            if (xtype == null)
            {
                type = type.Substring(type.LastIndexOf(".") + 1);
                xtype = assembly.GetType(Config.EntityModule + ".Entities." + type);
            }
            System.Reflection.PropertyInfo[] propertyList = xtype.GetProperties();

            foreach (System.Reflection.PropertyInfo item in propertyList)
            {
                JPropertyInfo temp = new JPropertyInfo();
                temp.PropertyName = item.Name;
                list.Add(temp);
            }
            return list;
        }
        private List<string> GetEntityList(string path)
        {
            List<string> list = null;
            if (Directory.Exists(path + "//Entities"))
            {
                list = new List<string>();
                foreach (var item in Directory.GetFiles(path + "//Entities"))
                {
                    FileInfo fileInfo = new FileInfo(item);
                    list.Add(fileInfo.Name.Replace(fileInfo.Extension, ""));
                }
            }
            return list;
        }
        private List<string> GetTemplateList(string path)
        {
            List<string> list = null;
            if (Directory.Exists(path + "Templates//Components"))
            {
                list = new List<string>();
                foreach (var item in Directory.GetFiles(path + "Templates//Components"))
                {
                    FileInfo fileInfo = new FileInfo(item);
                    list.Add(fileInfo.Name);
                }
            }
            return list;
        }
        #endregion

        public JsonResult GetViewList(string stateName, string stateName2)
        {
            JSONData res = new JSONData();
            res.success = true;
            StateManager sm = new StateManager();
            sm.RootPath = Config.Root;
            sm.Deserialize();
            State temp = sm.StateList.FirstOrDefault(x => x.StateName ==stateName);
            if (temp == null)
            {
                res.success = false;
            }
            else
            {
                try
                {
                    string input = System.IO.File.ReadAllText(Config.Root + "Templates\\Layouts\\" + temp.TemplateUrl);
                    var matches = Regex.Matches(input, "ui-view=\"([a-zA-Z0-9]+)\"", RegexOptions.IgnoreCase);
                    List<string> views = new List<string>();
                    foreach (Match item in matches)
                    {                      
                        views.Add(item.Groups[1].Value);
                    }
                    State temp2 = sm.StateList.FirstOrDefault(x => x.StateName == stateName2);
                    if (temp2 == null)
                    {
                        temp2 = new State();
                        foreach (string item2 in views)
                        {                            
                           temp2.StateViews.Add(new StateView { ViewName = item2, ControllerName = "", TemplateUrl = "" });
                           
                        }
                        res.data = temp2.StateViews;
                    }
                    else if (temp2.StateViews.Count == 0)
                    {
                        foreach (var item in views)
                        {
                            temp2.StateViews.Add(new StateView { ViewName = item, ControllerName = "", TemplateUrl = "" });
                        }
                        res.data = temp2.StateViews;
                    }
                    else
                    {
                        
                        foreach (string item2 in views)
                        {
                            var ld = temp2.StateViews.FirstOrDefault(v => v.ViewName == item2);
                            if (ld == null)
                            {
                                temp2.StateViews.Add(new StateView { ViewName = item2, ControllerName = "", TemplateUrl = "" });
                            }
                        }
                        res.data = temp2.StateViews;
                    }
                }
                catch (Exception ex)
                {
                    res.success = false;
                }
            }
           
           
            
            return Json(res, JsonRequestBehavior.AllowGet);
        }
    }
    public class JSONData
    {
        public bool success { get; set; }
        public dynamic data { get; set; }
        public string message { get; set; }
    }
    public class JPropertyInfo
    {
        public bool IsReq { get; set; }
        public bool IsMin { get; set; }
        public bool IsMax { get; set; }
        public int MinLength { get; set; }
        public int MaxLength { get; set; }

        public string MinMsg { get; set; }
        public string MaxMsg { get; set; }
        public string ReqMsg { get; set; }

        public bool Checked { get; set; }
        public string PropertyName { get; set; }
        public bool HasDetail { get; set; }
        public string Xtype { get; set; }

        public string UiType { get; set; }
        public List<JPropertyInfo> Details { get; set; }
    }
    public class JwtWidget
    {
        public string Name { get; set; }
        public List<JPropertyInfo> PropertyList { get; set; }
    }
}
