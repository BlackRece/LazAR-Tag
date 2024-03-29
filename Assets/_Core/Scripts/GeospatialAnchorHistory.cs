namespace BlackRece.LaSARTag.Geospatial
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// A serializable struct that stores the basic information of a persistent geospatial anchor.
    /// </summary>
    [Serializable]
    public struct GeospatialAnchorHistory
    {
        /// <summary>
        /// The created time of this geospatial anchor.
        /// </summary>
        public string SerializedTime;

        /// <summary>
        /// Latitude of the creation pose in degrees.
        /// </summary>
        public double Latitude;

        /// <summary>
        /// Longitude of the creation pose in degrees.
        /// </summary>
        public double Longitude;

        /// <summary>
        /// Altitude of the creation pose in meters above the WGS84 ellipsoid.
        /// </summary>
        public double Altitude;

        /// <summary>
        /// Heading of the creation pose in degrees, used to calculate the original orientation.
        /// </summary>
        public double Heading;

        /// <summary>
        /// Rotation of the creation pose as a quaternion, used to calculate the original
        /// orientation.
        /// </summary>
        public Quaternion EunRotation;

        /// <summary>
        /// Construct a Geospatial Anchor history.
        /// </summary>
        /// <param name="time">The time this Geospatial Anchor was created.</param>
        /// <param name="latitude">
        /// Latitude of the creation pose in degrees.</param>
        /// <param name="longitude">
        /// Longitude of the creation pose in degrees.</param>
        /// <param name="altitude">
        /// Altitude of the creation pose in meters above the WGS84 ellipsoid.</param>
        /// <param name="eunRotation">
        /// Rotation of the creation pose as a quaternion, used to calculate the original
        /// orientation.
        /// </param>
        public GeospatialAnchorHistory(DateTime time, double latitude, double longitude,
            double altitude, Quaternion eunRotation)
        {
            SerializedTime = time.ToString();
            Latitude = latitude;
            Longitude = longitude;
            Altitude = altitude;
            Heading = 0.0f;
            EunRotation = eunRotation;
        }

        /// <summary>
        /// Construct a Geospatial Anchor history.
        /// </summary>
        /// <param name="latitude">
        /// Latitude of the creation pose in degrees.</param>
        /// <param name="longitude">
        /// Longitude of the creation pose in degrees.</param>
        /// <param name="altitude">
        /// Altitude of the creation pose in meters above the WGS84 ellipsoid.</param>
        /// <param name="eunRotation">
        /// Rotation of the creation pose as a quaternion, used to calculate the original
        /// orientation.
        /// </param>
        public GeospatialAnchorHistory(
            double latitude, double longitude, double altitude, Quaternion eunRotation) :
            this(DateTime.Now, latitude, longitude, altitude, eunRotation)
        {
        }

        /// <summary>
        /// Gets created time in DataTime format.
        /// </summary>
        public DateTime CreatedTime => Convert.ToDateTime(SerializedTime);

        /// <summary>
        /// Overrides ToString() method.
        /// </summary>
        /// <returns>Return the json string of this object.</returns>
        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }
    }

    /// <summary>
    /// A wrapper class for serializing a collection of <see cref="GeospatialAnchorHistory"/>.
    /// </summary>
    [Serializable]
    public class GeospatialAnchorHistoryCollection
    {
        /// <summary>
        /// A list of Geospatial Anchor History Data.
        /// </summary>
        public List<GeospatialAnchorHistory> Collection = new List<GeospatialAnchorHistory>();
    }
}
