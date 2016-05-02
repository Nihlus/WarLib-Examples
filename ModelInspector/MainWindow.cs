//
//  MainWindow.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2016 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
using System;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;
using Warcraft.MDX;
using System.IO;
using ColladaSharp.Collada;
using System.Collections.Generic;
using ColladaSharp.Common.Model;
using Warcraft.MDX.Geometry;
using Warcraft.Core;
using ColladaSharp.Collada.Elements.Geometry.GeometryTypes;

public partial class MainWindow: Gtk.Window
{
	Builder builder;

	[UI] Gtk.ToolButton OpenFileButton;
	[UI] Gtk.FileChooserDialog FileChooser;

	public static MainWindow Create()
	{
		Builder builder = new Builder(null, "ModelInspector.interfaces.MainWindow.glade", null);
		return new MainWindow(builder, builder.GetObject("window1").Handle);
	}

	protected MainWindow(Builder builder, IntPtr handle)
		: base(handle)
	{
		this.builder = builder;

		builder.Autoconnect(this);
		DeleteEvent += OnDeleteEvent;

		OpenFileButton.Clicked += OnOpenFileButtonClicked;
	}

	protected void OnOpenFileButtonClicked(object sender, EventArgs e)
	{
		if (FileChooser.Run() == (int)ResponseType.Accept)
		{
			if (File.Exists(FileChooser.Filename))
			{
				using (FileStream file = File.OpenRead(FileChooser.Filename))
				{
					MDX model = new MDX(file);

					ColladaModel colladaModel = new ColladaModel();

					// Extract all vertices from the MDX model
					List<Vertex> Vertices = new List<Vertex>();
					foreach (MDXVertex mdxVertex in model.Vertices)
					{
						Vertex vertex = new Vertex(mdxVertex.Position.X, mdxVertex.Position.Y, mdxVertex.Position.Z);
						vertex.VertexNormal = new Normal(mdxVertex.Normal.X, mdxVertex.Normal.Y, mdxVertex.Normal.Z);
						Vertices.Add(vertex);
					}

					// Extract all trianges from LOD 0 of the MDX model
					List<Polygon> Triangles = new List<Polygon>();
					foreach (MDXTriangle Triangle in model.LODViews[0].Triangles)
					{
						Polygon polygon = new Polygon();
						polygon.VertexIndices = new List<int>();

						polygon.VertexIndices.Add(Triangle.VertexA);
						polygon.VertexIndices.Add(Triangle.VertexB);
						polygon.VertexIndices.Add(Triangle.VertexC);

						Triangles.Add(polygon);
					}

					// Extract all face normals from the MDX model
					List<Normal> Normals = new List<Normal>();
					int normalCount = 0;
					foreach (Polygon Triangle in Triangles)
					{
						List<Vector3f> VertexPositions = new List<Vector3f>();
						List<Vector3f> VertexNormals = new List<Vector3f>();

						foreach (int vertexIndex in Triangle.VertexIndices)
						{
							VertexPositions.Add(new Vector3f(Vertices[vertexIndex].X, Vertices[vertexIndex].Y, Vertices[vertexIndex].Z));
							VertexNormals.Add(new Vector3f(Vertices[vertexIndex].X, Vertices[vertexIndex].Y, Vertices[vertexIndex].Z));
						}

						Vector3f faceNormal = CalculateFaceNormal(VertexPositions, VertexNormals);

						Normals.Add(new Normal(faceNormal.X, faceNormal.Y, faceNormal.Z));
						Triangle.FaceNormalIndex = normalCount;

						normalCount++;
					}

					ColladaMesh mesh = colladaModel.AddMesh(model.Name);
					mesh.AddVertices(Vertices);
					mesh.AddPolygons(Triangles);
					mesh.AddNormals(Normals);

					/*
						Dirty debug OBJ export
					*/

					string modelPath = String.Format("{0}.obj", model.Name.Replace("\0", String.Empty));
					using (FileStream fs = File.Create(modelPath))
					{
						using (StreamWriter sw = new StreamWriter(fs))
						{	
							// Write the vertices
							foreach (ushort vertex in model.LODViews[0].VertexIndices)
							{
								MDXVertex globalVertex = model.Vertices[vertex];

								sw.WriteLine(String.Format("v {0} {1} {2}", globalVertex.Position.X, globalVertex.Position.Y, globalVertex.Position.Z));
							}

							Dictionary<ushort, ushort> NormalMappingTable = new Dictionary<ushort, ushort>();

							// Write the vertex normals
							ushort normalIndex = 0;
							foreach (ushort vertexIndex in model.LODViews[0].VertexIndices)
							{
								MDXVertex globalVertex = model.Vertices[vertexIndex];

								NormalMappingTable.Add(vertexIndex, (ushort)(normalIndex + 1));

								sw.WriteLine(String.Format("vn {0} {1} {2}", globalVertex.Normal.X, globalVertex.Normal.Y, globalVertex.Normal.Z));

								++normalIndex;
							}

							// Write the triangles
							foreach (MDXTriangle Triangle in model.LODViews[0].Triangles)
							{
								ushort vertexIndexA = Triangle.VertexA;
								ushort vertexIndexB = Triangle.VertexB;
								ushort vertexIndexC = Triangle.VertexC;

								ushort globalVertexIndexA = (ushort)(model.LODViews[0].VertexIndices[vertexIndexA] + 1);
								ushort globalVertexIndexB = (ushort)(model.LODViews[0].VertexIndices[vertexIndexB] + 1);
								ushort globalVertexIndexC = (ushort)(model.LODViews[0].VertexIndices[vertexIndexC] + 1);

								sw.WriteLine(String.Format("f {0}//{3} {1}//{4} {2}//{5}", globalVertexIndexA, globalVertexIndexB, globalVertexIndexC,
										NormalMappingTable[vertexIndexA], NormalMappingTable[vertexIndexB], NormalMappingTable[vertexIndexC]));
							}
						}
					}
				}
			}
		}

		FileChooser.Hide();
	}

	private Vector3f CalculateFaceNormal(List<Vector3f> VertexPositions, List<Vector3f> VertexNormals)
	{
		Vector3f p0 = VertexPositions[1] - VertexPositions[0];
		Vector3f p1 = VertexPositions[2] - VertexPositions[0];
		Vector3f faceNormal = Vector3f.Cross(p0, p1);

		Vector3f vertexNormalAverage = (VertexNormals[0] + VertexNormals[1] + VertexNormals[2]) / 3;
		float dot = Vector3f.Dot(faceNormal, vertexNormalAverage);

		return (dot < 0.0f) ? -faceNormal : faceNormal;
	}

	protected void OnDeleteEvent(object sender, DeleteEventArgs a)
	{
		Application.Quit();
		a.RetVal = true;
	}
}
