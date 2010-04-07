﻿#region usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Collections;
using System.Linq.Expressions;
using System.Web.Mvc.Html;
using Signum.Entities;
using Signum.Entities.Reflection;
using Signum.Utilities;
using System.Configuration;
using Signum.Web.Properties;
using Signum.Engine;
#endregion

namespace Signum.Web
{
    public static class EntityRepeaterHelper
    {
        private static void InternalEntityRepeater<T>(this HtmlHelper helper, TypeContext<MList<T>> typeContext, EntityRepeater settings)
        {
            if (!settings.Visible || settings.HideIfNull && typeContext.Value == null)
                return;

            string prefix = helper.GlobalName(typeContext.Name);
            MList<T> value = typeContext.Value;  
            Type elementsCleanStaticType = Reflector.ExtractLite(typeof(T)) ?? typeof(T);

            long? ticks = EntityBaseHelper.GetTicks(helper, prefix, settings);

            StringBuilder sb = new StringBuilder();

            sb.AppendLine(helper.HiddenStaticInfo(prefix, new StaticInfo(elementsCleanStaticType) { IsReadOnly = settings.ReadOnly }));
            sb.AppendLine(helper.Hidden(TypeContext.Compose(prefix, TypeContext.Ticks), ticks.TryToString() ?? ""));
            
            sb.AppendLine(EntityBaseHelper.WriteImplementations(helper, settings, prefix));

            sb.AppendLine(EntityBaseHelper.WriteLabel(helper, prefix, settings));

            //If it's an embeddedEntity write an empty template with index 0 to be used when creating a new item
            if (typeof(EmbeddedEntity).IsAssignableFrom(elementsCleanStaticType))
                sb.AppendLine("<script type=\"text/javascript\">var {0} = \"{1}\";</script>".Formato(
                        TypeContext.Compose(prefix, EntityBaseKeys.Template),
                        EntityBaseHelper.JsEscape(ListBaseHelper.RenderItemContent(helper, prefix, typeContext, (T)(object)Constructor.Construct(typeof(T)), 0, settings, elementsCleanStaticType, elementsCleanStaticType, typeof(Lite).IsAssignableFrom(typeof(T))))));

            sb.AppendLine(ListBaseHelper.WriteCreateButton(helper, settings, new Dictionary<string, object>{{"title", settings.AddElementLinkText}}));
            sb.AppendLine(ListBaseHelper.WriteFindButton(helper, settings, elementsCleanStaticType));

            sb.AppendLine(helper.Div("", "", "clearall", null)); //To keep create and find buttons' space

            sb.AppendLine("<div id='{0}' name='{0}'>".Formato(TypeContext.Compose(prefix, EntityRepeaterKeys.ItemsContainer)));
            if (value != null)
            {
                for (int i = 0; i < value.Count; i++)
                    sb.Append(InternalRepeaterElement(helper, prefix, value[i], i, settings, typeContext));
            }
            sb.AppendLine("</div>");

            sb.AppendLine(EntityBaseHelper.WriteBreakLine());

            helper.ViewContext.HttpContext.Response.Write(sb.ToString());
        }

        private static string InternalRepeaterElement<T>(this HtmlHelper helper, string prefix, T value, int index, EntityRepeater settings, TypeContext<MList<T>> typeContext)
        {
            string indexedPrefix = TypeContext.Compose(prefix, index.ToString());
            Type cleanStaticType = Reflector.ExtractLite(typeof(T)) ?? typeof(T);
            bool isIdentifiable = typeof(IdentifiableEntity).IsAssignableFrom(typeof(T));
            bool isLite = typeof(Lite).IsAssignableFrom(typeof(T));
            
            Type cleanRuntimeType = null;
            if (value != null)
                cleanRuntimeType = typeof(Lite).IsAssignableFrom(value.GetType()) ? (value as Lite).RuntimeType : value.GetType();

            long? ticks = EntityBaseHelper.GetTicks(helper, indexedPrefix, settings);

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("<div id='{0}' name='{0}' class='repeaterElement'>".Formato(TypeContext.Compose(indexedPrefix, EntityRepeaterKeys.RepeaterElement)));
            
            sb.AppendLine(helper.Hidden(TypeContext.Compose(indexedPrefix, EntityListBaseKeys.Index), index.ToString()));

            sb.AppendLine(helper.HiddenRuntimeInfo(indexedPrefix, new RuntimeInfo(value) { Ticks = ticks }));

            //if (isIdentifiable || isLite)
            //    sb.AppendLine(helper.HiddenRuntimeInfo(indexedPrefix, new RuntimeInfo<T>(value) { Ticks = ticks }));
            //else
            //    sb.AppendLine(helper.HiddenRuntimeInfo(indexedPrefix, new EmbeddedRuntimeInfo<T>(value, false) { Ticks = ticks }));

            if (settings.Remove)
                sb.AppendLine(
                    helper.Button(TypeContext.Compose(indexedPrefix, "btnRemove"),
                                  "x",
                                  "ERepOnRemoving({0}, '{1}');".Formato(settings.ToJS(), indexedPrefix),
                                  "lineButton remove",
                                  new Dictionary<string, object> { { "title", settings.RemoveElementLinkText } }));

            sb.AppendLine(ListBaseHelper.RenderItemContentInEntityDiv(helper, indexedPrefix, typeContext, value, index, settings, cleanRuntimeType, cleanStaticType, isLite, true));

            sb.AppendLine("</div>");

            return sb.ToString();
        }

        public static void EntityRepeater<T, S>(this HtmlHelper helper, TypeContext<T> tc, Expression<Func<T, MList<S>>> property)
            where S : Modifiable 
        {
            helper.EntityRepeater(tc, property, null);
        }

        public static void EntityRepeater<T, S>(this HtmlHelper helper, TypeContext<T> tc, Expression<Func<T, MList<S>>> property, Action<EntityRepeater> settingsModifier)
        {
            TypeContext<MList<S>> context = Common.WalkExpression(tc, property);

            EntityRepeater el = new EntityRepeater(helper.GlobalName(context.Name));
            Navigator.ConfigureEntityBase(el, Reflector.ExtractLite(typeof(S)) ?? typeof(S), false);
            Common.FireCommonTasks(el, context);

            if (settingsModifier != null)
                settingsModifier(el);

            using (el)
                helper.InternalEntityRepeater<S>(context, el);
        }
    }
}
