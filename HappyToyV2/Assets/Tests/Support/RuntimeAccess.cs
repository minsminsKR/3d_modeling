using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HappyToy.V2.CloudTests
{
    // UTF asmdefs cannot reference predefined Assembly-CSharp. This bridge invokes the
    // real imported game assembly without moving scripts or changing scene type identities.
    public static class RuntimeAccess
    {
        public const string ScenePath = "Assets/Annex/SchoolAnnex.unity";
        public static Type RequireType(string name, string assembly = "Assembly-CSharp")
        {
            var type = Type.GetType("HappyToy.V2." + name + ", " + assembly, false);
            Assert.That(type, Is.Not.Null, "Missing real project type " + name + " in " + assembly);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo(assembly));
            return type;
        }
        public static Component[] Components(string name, bool includeInactive = true)
        {
            var type = RequireType(name);
            return SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren(type, includeInactive)).Cast<Component>().ToArray();
        }
        public static Component One(string name)
        {
            var found = Components(name);
            Assert.That(found.Length, Is.EqualTo(1), "Expected exactly one authored " + name);
            return found[0];
        }
        public static T Get<T>(object target, string name)
        {
            Assert.That(target, Is.Not.Null, "Missing target for " + name);
            var type = target.GetType();
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null) return (T)property.GetValue(target);
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing public member " + type.Name + "." + name);
            return (T)field.GetValue(target);
        }
        public static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing public field " + target.GetType().Name + "." + name);
            field.SetValue(target, field.FieldType.IsEnum && value is string text ? Enum.Parse(field.FieldType, text) : value);
        }
        public static object Call(object target, string name, params object[] arguments)
        {
            Assert.That(target, Is.Not.Null, "Missing invocation target " + name);
            Type type = target as Type ?? target.GetType();
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(method => method.Name == name && method.GetParameters().Length == arguments.Length).ToArray();
            Assert.That(methods.Length, Is.EqualTo(1), "Expected one public method " + type.Name + "." + name);
            try { return methods[0].Invoke(target is Type ? null : target, arguments); }
            catch (TargetInvocationException error)
            {
                if (error.InnerException != null) ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }
        public static IEnumerator Wait(Func<bool> condition, float seconds, string failure, Func<string> diagnostics = null)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            if (!condition()) Assert.Fail(failure + (diagnostics == null ? string.Empty : "\n" + diagnostics()));
        }
        public static IEnumerator Delay(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
