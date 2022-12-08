using Stride.Core.Mathematics;
using Stride.Graphics;

namespace MarchingCubesImproved
{
    public class MarchingCubes
    {
        private VertexPositionNormalTexture[] _vertices;
        private int[] _triangles;
        private float _isolevel;

        private int _vertexIndex;

        private VertexPositionNormalTexture[] _vertexList;
        private Point[] _initPoints;
        private int[,,] _cubeIndexes;
        
        public MarchingCubes(Point[,,] points, float isolevel, int seed)
        {
            _isolevel = isolevel;

            _vertexIndex = 0;

            _vertexList = new VertexPositionNormalTexture[12];
            _initPoints = new Point[8];
            _cubeIndexes = new int[points.GetLength(0) - 1, points.GetLength(1) - 1, points.GetLength(2) - 1];
        }

        private Vector3 VertexInterpolate(Vector3 p1, Vector3 p2, float v1, float v2)
        {
            if (MathHelpers.Abs(_isolevel - v1) < 0.000001f)
            {
                return p1;
            }

            if (MathHelpers.Abs(_isolevel - v2) < 0.000001f)
            {
                return p2;
            }

            if (MathHelpers.Abs(v1 - v2) < 0.000001f)
            {
                return p1;
            }

            float mu = (_isolevel - v1) / (v2 - v1);

            Vector3 p = p1 + mu * (p2 - p1);

            return p;
        }

        private void March(Point[] points, int cubeIndex)
        {
            int edgeIndex = LookupTables.EdgeTable[cubeIndex];

            _vertexList = GenerateVertexList(points, edgeIndex);

            int[] row = LookupTables.TriangleTable[cubeIndex];

            for (int i = 0; i < row.Length; i += 3)
            {
                var vertA = _vertexList[row[i + 2]];
                var vertB = _vertexList[row[i + 1]];
                var vertC = _vertexList[row[i + 0]];
                
                var norm = Vector3.Cross(
                    vertA.Position - vertB.Position,
                    vertC.Position - vertB.Position
                );

                _vertices[_vertexIndex] = vertA;
                _vertices[_vertexIndex].Normal = norm;
                _triangles[_vertexIndex] = _vertexIndex;
                _vertexIndex++;

                _vertices[_vertexIndex] = vertB;
                _vertices[_vertexIndex].Normal = norm;
                _triangles[_vertexIndex] = _vertexIndex;
                _vertexIndex++;

                _vertices[_vertexIndex] = vertC;
                _vertices[_vertexIndex].Normal = norm;
                _triangles[_vertexIndex] = _vertexIndex;
                _vertexIndex++;
            }
        }

        private VertexPositionNormalTexture[] GenerateVertexList(Point[] points, int edgeIndex)
        {
            for (int i = 0; i < 12; i++)
            {
                if ((edgeIndex & (1 << i)) != 0)
                {
                    int[] edgePair = LookupTables.EdgeIndexTable[i];
                    int edge1 = edgePair[0];
                    int edge2 = edgePair[1];

                    Point point1 = points[edge1];
                    Point point2 = points[edge2];

                    _vertexList[i].Position = VertexInterpolate(point1.LocalPosition, point2.LocalPosition,
                        point1.Density, point2.Density);
                    // TODO Ideally we want to use triplanar mapping or a better way of working out the UV positions..
                    _vertexList[i].TextureCoordinate = UVs[i % 3];
                    _vertexList[i].Normal = Vector3.Zero;
                }
            }

            return _vertexList;
        }

        private static readonly Vector2[] UVs = new Vector2[3]
        {
            new Vector2(0, 0),
            new Vector2(0, 1),
            new Vector2(1, 0),
        };


        private int CalculateCubeIndex(Point[] points, float iso)
        {
            int cubeIndex = 0;

            for (int i = 0; i < 8; i++)
                if (points[i].Density < iso)
                    cubeIndex |= 1 << i;

            return cubeIndex;
        }

        public (VertexPositionNormalTexture[], int[]) CreateMeshData(Point[,,] points)
        {
            _cubeIndexes = GenerateCubeIndexes(points);
            int vertexCount = GenerateVertexCount(_cubeIndexes);

            if (vertexCount <= 0)
            {
                return (null, null);
            }

            _vertices = new VertexPositionNormalTexture[vertexCount];
            _triangles = new int[vertexCount];

            for (int x = 0; x < points.GetLength(0) - 1; x++)
            {
                for (int y = 0; y < points.GetLength(1) - 1; y++)
                {
                    for (int z = 0; z < points.GetLength(2) - 1; z++)
                    {
                        int cubeIndex = _cubeIndexes[x, y, z];
                        if (cubeIndex == 0 || cubeIndex == 255) continue;

                        March(GetPoints(x, y, z, points), cubeIndex);
                    }
                }
            }

            _vertexIndex = 0;

            return (_vertices, _triangles);
        }

        private Point[] GetPoints(int x, int y, int z, Point[,,] points)
        {
            for (int i = 0; i < 8; i++)
            {
                Point p = points[x + CubePointsX[i], y + CubePointsY[i], z + CubePointsZ[i]];
                _initPoints[i] = p;
            }

            return _initPoints;
        }

        private int[,,] GenerateCubeIndexes(Point[,,] points)
        {
            for (int x = 0; x < points.GetLength(0) - 1; x++)
            {
                for (int y = 0; y < points.GetLength(1) - 1; y++)
                {
                    for (int z = 0; z < points.GetLength(2) - 1; z++)
                    {
                        _initPoints = GetPoints(x, y, z, points);

                        _cubeIndexes[x, y, z] = CalculateCubeIndex(_initPoints, _isolevel);
                    }
                }
            }

            return _cubeIndexes;
        }

        private int GenerateVertexCount(int[,,] cubeIndexes)
        {
            int vertexCount = 0;

            for (int x = 0; x < cubeIndexes.GetLength(0); x++)
            {
                for (int y = 0; y < cubeIndexes.GetLength(1); y++)
                {
                    for (int z = 0; z < cubeIndexes.GetLength(2); z++)
                    {
                        int cubeIndex = cubeIndexes[x, y, z];
                        int[] row = LookupTables.TriangleTable[cubeIndex];
                        vertexCount += row.Length;
                    }
                }
            }

            return vertexCount;
        }

        public static readonly Vector3Int[] CubePoints =
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(1, 0, 1),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 1, 0),
            new Vector3Int(1, 1, 0),
            new Vector3Int(1, 1, 1),
            new Vector3Int(0, 1, 1)
        };

        public static readonly int[] CubePointsX =
        {
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0
        };

        public static readonly int[] CubePointsY =
        {
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1
        };

        public static readonly int[] CubePointsZ =
        {
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1
        };
    }
}