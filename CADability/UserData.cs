﻿using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability
{
    interface IUserData
    {
        UserData UserData { get; }
    }

    /// <summary>
    /// To control the behaviour of <see cref="UserData"/> objects the value part of a UserData
    /// may implement this interface. You can then control the cloning and representation of
    /// your object.
    /// ------------------- NICHT MEHR VERWENDEN -------------------
    /// </summary>
    internal interface IManageUserData
    {
        /// <summary>
        /// The <see cref="UserData"/> object is beeing cloned. return an appropriate clone
        /// of your object or null if it shouldnt be cloned
        /// </summary>
        /// <returns>the clone or null</returns>
        object Clone();
        /// <summary>
        /// The <see cref="UserData"/> object can be displayed in the control center if it returns
        /// a <see cref="IShowProperty"/> interface. All details of representation are managed in the
        /// IShowProperty interface. Return null, if the object should be invisible to the user.
        /// </summary>
        /// <returns>IShowProperty interface or null</returns>
        IShowProperty GetShowProperty();
        void Overriding(IManageUserData newValue);
    }


    /// <summary>
    /// Implement this interface on your UserData objects if you want your UserData to be displayed
    /// as a common property of multiple selected objects.
    /// </summary>

    public interface IMultiObjectUserData
    {
        IPropertyEntry GetShowProperty(GeoObjectList selectedObjects);
        bool isChanging { get; }
    }

    /// <summary>
    /// A table, that associates names with objects.
    /// Its purpose is to attach any kind of (user) information to existing CADability objects.
    /// Many objects of the CADability namespace provide a UserData property, by means of which
    /// you can connect any object to it. If the object is serializable, it will be serialized
    /// together with the CADability object. If it implements IClonable it will be cloned when the
    /// containing object is cloned. If it implements <see cref="IShowProperty"/> it will be displayed
    /// together with the object in the ControlCenter. If it implements <see cref="IMultiObjectUserData"/>
    /// it will be displayed as a common property when multiple objects are displayed in the ControlCenter
    /// </summary>
    [Serializable()]
    public class UserData : ISerializable, IDictionary<string, object>, IDictionary, IJsonSerialize
    {
        public delegate void UserDataAddedDelegate(string name, object value);
        public delegate void UserDataRemovedDelegate(string name, object value);
        public event UserDataAddedDelegate UserDataAddedEvent;
        public event UserDataRemovedDelegate UserDataRemovedEvent;
        private Dictionary<string, object> data; // das hält die Daten
        public UserData()
        {
            data = new Dictionary<string, object>();
        }
        internal void CloneFrom(UserData toClone)
        {
            if (toClone == null) return;
            foreach (KeyValuePair<string, object> de in toClone.data)
            {
                if (de.Key == "CADability.Path.Original")
                {   // sonst würde das GeoObject gecloned, wir wollen aber explizizt die Referenz erhalten
                    // das müsste noch sauber spezifiziert werden
                    Add(de.Key, de.Value);
                }
                else if (de.Key == "BRepIntersection.OverlapsWith")
                {   // sonst Endlosrekursion beim clonen in BRep Operationen
                    // (wird nur in Debug verwendet
                    Add(de.Key, de.Value);
                }
                else if (de.Value is IManageUserData)
                {
                    object o = (de.Value as IManageUserData).Clone();
                    if (o != null) Add(de.Key, o);
                }
                else if (de.Value is ICloneable)
                {
                    object o = (de.Value as ICloneable).Clone();
                    if (o != null) Add(de.Key, o);
                }
                else
                {   // sicher? immer clonen, auch wenn kein IManageUserData?
                    // wird zumindes bei "3d" so gebraucht, wenn hier raus, dann dort (CurveGraph) ändern
                    // Kunden verwenden das auch in diesem Sinn, ebenso Make3D.CloneFaceUserData
                    Add(de.Key, de.Value);
                }
            }
        }
        /// <summary>
        /// Returns a clone of the UserData object. The containe dictionary of string-object pairs is cloned so the result is
        /// independant from this object. The values are also cloned if they implement the <see cref="ICloneable"/> interface.
        /// If the value of an entry implements <see cref="IManageUserData"/>, <see cref="IManageUserData.Clone"/> will be called.
        /// </summary>
        /// <returns>The cloned UserData</returns>
        public UserData Clone()
        {
            UserData res = new UserData();
            res.CloneFrom(this);
            return res;
        }
        /// <summary>
        /// Adds or replaces the named entry of the Userdata
        /// </summary>
        /// <param name="Name">eindeutige Bezeichnung des Objektes</param>
        /// <param name="Data">das zusätzliche Objekt</param>
        public void Add(string Name, object Data)
        {
            object found;
            if (data.TryGetValue(Name, out found))
            {
                IManageUserData mud = found as IManageUserData;
                if (mud != null) mud.Overriding(Data as IManageUserData);
            }
            data[Name] = Data;
            if (UserDataAddedEvent != null) UserDataAddedEvent(Name, Data);
        }
        /// <summary>
        /// Add all entries given in the parameter to this UserData
        /// </summary>
        /// <param name="userData">entries to add</param>
        public void Add(UserData userData)
        {
            foreach (KeyValuePair<string, object> de in userData.data)
            {
                data[de.Key] = de.Value;
                if (UserDataAddedEvent != null) UserDataAddedEvent(de.Key, de.Value);
            }
        }
        /// <summary>
        /// Indexer to read or write values to a given name
        /// </summary>
        public object this[string Name]
        {
            get
            {
                object res;
                data.TryGetValue(Name, out res); // res wird null wenn nicht gefunden
                return res;
            }
            set
            {
                data[Name] = value;
            }
        }
        /// <summary>
        /// Removes the entry with the provided name
        /// </summary>
        /// <param name="Name">Name of the entry to be removed.</param>
        public void RemoveUserData(string Name)
        {
            object value;
            if (data.TryGetValue(Name, out value))
            {
                data.Remove(Name);
                if (UserDataRemovedEvent != null) UserDataRemovedEvent(Name, value);
            }
        }
        /// <summary>
        /// Checks whether an entry with the provided name exists.
        /// </summary>
        /// <param name="Name">Name to check</param>
        /// <returns>true if the entry exists</returns>
        public bool ContainsData(string Name)
        {
            return data.ContainsKey(Name);
        }
        /// <summary>
        /// Returns the entry with the provided name. The result may be null if the entry doesn't exist.
        /// The result must be casted to the required type.
        /// </summary>
        /// <param name="Name">Name of the entry</param>
        /// <returns></returns>
        public object GetData(string Name)
        {
            object res;
            data.TryGetValue(Name, out res);
            return res;
        }
        /// <summary>
        /// Gets an array of the names of all entries
        /// </summary>
        public string[] AllItems
        {
            get
            {
                string[] res = new string[data.Keys.Count];
                data.Keys.CopyTo(res, 0);
                return res;
            }
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected UserData(SerializationInfo info, StreamingContext context)
        {
            data = new Dictionary<string, object>();
            // hier wird etwas umständlich alles wieder gelesen.
            // wo müsste man das ExceptionHandling anbringen, um nicht
            // wiederherstellbare objekte zu überspringen?
            if (info.MemberCount > 0)
            {
                SerializationInfoEnumerator sie = info.GetEnumerator();
                while (sie.MoveNext())
                {
                    data[sie.Current.Name] = info.GetValue(sie.Current.Name, sie.Current.ObjectType);
                    // sie.Current.Value geht nicht, weil das ist bei den einfachen Type (int, double, bool)
                    // immer string, deshalb hier etwas umständlich. (gilt nur für xml, bei binary würde es gehen)
                }
            }
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            foreach (KeyValuePair<string, object> de in data)
            {	// es wird alles so geschrieben und nicht die Hashtable direkt abgespeichert
                // damit man beim Einlesen bessere Möglichkeiten hat, Fehler zu unterdrücken
                if (de.Value != null && !de.Value.GetType().IsGenericType)
                {
                    // so kann man feststellen, ob de.Value serialisierbar ist
                    SerializableAttribute sa = (SerializableAttribute)System.Attribute.GetCustomAttribute(de.Value.GetType(), typeof(SerializableAttribute));
                    if (sa != null)
                    {
                        info.AddValue(de.Key, de.Value, de.Value.GetType());
                    }
                    else if (de.Value is object[])
                    {
                        info.AddValue(de.Key, de.Value, de.Value.GetType()); // geht auch so bei object[]
                        //object[] oa = de.Value as object[];
                        //bool isString = true;
                        //for (int i = 0; i < oa.Length; i++)
                        //{
                        //    if (!(oa[i] is string)) isString = false;
                        //}
                        //if (isString)
                        //{
                        //    string[] sta = new string[oa.Length];
                        //    for (int i = 0; i < oa.Length; i++)
                        //    {
                        //        sta[i] = oa[i] as string;
                        //    }
                        //    SerializableAttribute dbg = (SerializableAttribute)System.Attribute.GetCustomAttribute(sta.GetType(), typeof(SerializableAttribute));
                        //    info.AddValue(de.Key, sta, sta.GetType());
                        //}
                    }
                    else if (de.Value.GetType().IsArray)
                    {
                        sa = (SerializableAttribute)System.Attribute.GetCustomAttribute(de.Value.GetType().GetElementType(), typeof(SerializableAttribute));
                        if (sa != null)
                        {
                            info.AddValue(de.Key, de.Value, de.Value.GetType());
                        }
                    }
#if DEBUG
                    if (de.Key == "SortingIndex") System.Diagnostics.Trace.WriteLine("Writing: SortingIndex: " + de.Value.ToString());
#endif

                }
            }
        }

        #endregion

        #region IDictionary Members

        public IDictionaryEnumerator GetEnumerator()
        {
            return data.GetEnumerator();
        }

        public void Remove(string key)
        {
            data.Remove(key);
        }

        public bool Contains(string key)
        {
            return data.ContainsKey(key);
        }

        public void Clear()
        {
            data.Clear();
        }

        public ICollection Values
        {
            get
            {
                return data.Values;
            }
        }

        public ICollection Keys
        {
            get
            {
                return data.Keys;
            }
        }

        public bool IsFixedSize
        {
            get
            {
                return false;
            }
        }

        #endregion

        public int Count
        {
            get
            {

                return data.Count;
            }
        }

        //#region IEnumerable Members

        //IEnumerator System.Collections.IEnumerable.GetEnumerator()
        //{
        //    return data.GetEnumerator();
        //}

        //#endregion

        #region IDictionary<string,object> Members

        void IDictionary<string, object>.Add(string key, object value)
        {
            this.Add(key, value);
        }

        bool IDictionary<string, object>.ContainsKey(string key)
        {
            return this.ContainsData(key);
        }

        ICollection<string> IDictionary<string, object>.Keys
        {
            get { return data.Keys; }
        }

        bool IDictionary<string, object>.Remove(string key)
        {
            return data.Remove(key);
        }

        bool IDictionary<string, object>.TryGetValue(string key, out object value)
        {
            return data.TryGetValue(key, out value);
        }

        ICollection<object> IDictionary<string, object>.Values
        {
            get { return data.Values; }
        }

        object IDictionary<string, object>.this[string key]
        {
            get
            {
                return data[key];
            }
            set
            {
                data[key] = value;
            }
        }

        #endregion

        #region ICollection<KeyValuePair<string,object>> Members

        void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
        {
            data.Add(item.Key, item.Value);
        }

        void ICollection<KeyValuePair<string, object>>.Clear()
        {
            data.Clear();
        }

        bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        int ICollection<KeyValuePair<string, object>>.Count
        {
            get { return data.Count; }
        }

        bool ICollection<KeyValuePair<string, object>>.IsReadOnly
        {
            get { return false; ; }
        }

        public bool IsReadOnly => ((IDictionary)data).IsReadOnly;

        public object SyncRoot => ((IDictionary)data).SyncRoot;

        public bool IsSynchronized => ((IDictionary)data).IsSynchronized;

        public object this[object key] { get => ((IDictionary)data)[key]; set => ((IDictionary)data)[key] = value; }

        bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion

        #region IEnumerable<KeyValuePair<string,object>> Members

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
        {
            return data.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return data.GetEnumerator();
        }

        public bool Contains(object key)
        {
            return ((IDictionary)data).Contains(key);
        }

        public void Add(object key, object value)
        {
            ((IDictionary)data).Add(key, value);
        }

        public void Remove(object key)
        {
            ((IDictionary)data).Remove(key);
        }

        public void CopyTo(Array array, int index)
        {
            ((IDictionary)data).CopyTo(array, index);
        }

        public void GetObjectData(IJsonWriteData data)
        {
            foreach (KeyValuePair<string, object> de in this.data)
            {	
                // since the keys must be unique, we use the keys as property names
                if (de.Value != null && !de.Value.GetType().IsGenericType && !de.Key.StartsWith("$")) // $xxx is reserved for Json additional entries
                {
                    // so kann man feststellen, ob de.Value serialisierbar ist
                    SerializableAttribute sa = (SerializableAttribute)System.Attribute.GetCustomAttribute(de.Value.GetType(), typeof(SerializableAttribute));
                    if (de.Value.GetType().IsPrimitive || de.Value is IJsonSerialize || sa != null)
                    {
                        data.AddProperty(de.Key, de.Value);
                    }
                    else if (de.Value.GetType().IsArray)
                    {
                        sa = (SerializableAttribute)System.Attribute.GetCustomAttribute(de.Value.GetType().GetElementType(), typeof(SerializableAttribute));
                        if (sa != null || de.Value.GetType().GetElementType().IsPrimitive || de.Value.GetType().GetElementType().GetInterface("IJsonSerialize") != null)
                        {
                            data.AddProperty(de.Key, de.Value);
                        }
                    }
                }
            }
        }
        public void SetObjectData(IJsonReadData data)
        {
            foreach (KeyValuePair<string,object> item in data)
            {
                if (!item.Key.StartsWith("$") && item.Key!="§Index") this.data[item.Key] = item.Value;
            }   
        }

        #endregion
    }

}
