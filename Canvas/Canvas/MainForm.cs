using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Canvas
{
    /// <summary>
    /// 도형 타입
    /// </summary>
    public enum ShapeType
    {
        Rectangle,
        Circle,
        Ellipse,
        Polygon
    }

    /// <summary>
    /// 도형 정보
    /// </summary>
    public class Shape
    {
        public ShapeType ShapeType { get; set; }
        // Polygon일 경우 정점 목록
        public List<PointF> Points { get; set; }
        // Rectangle / Circle / Ellipse 용 Bounds
        public RectangleF Bounds { get; set; }
        public Color Color { get; set; } = Color.DarkBlue;

        public Shape(ShapeType shapeType)
        {
            ShapeType = shapeType;
            Points = new List<PointF>();
        }
    }

    /// <summary>
    /// Resize(리사이즈) 핸들의 종류
    /// - Polygon 버텍스 편집을 위해 PolygonVertex 추가
    /// </summary>
    public enum ResizeHandleType
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        PolygonVertex
    }

    /// <summary>
    /// 메인 폼 (partial class)
    /// </summary>
    public partial class MainForm : Form
    {
        private FlowLayoutPanel _buttonPanel;
        private Button _btnAddRect;
        private Button _btnAddCircle;
        private Button _btnAddEllipse;
        private Button _btnAddPolygonN;
        private Button _btnDeleteShape;
        private Button _btnZoomIn;
        private Button _btnZoomOut;

        // 커서 좌표 표시용
        private Label _lblCursorPos;

        private DrawingPanel _drawingPanel;

        public MainForm()
        {
            this.Text = "Canvas Sample (Grid, Swap Logic, Polygon Editing, etc.)";
            this.Width = 1400;
            this.Height = 900;

            // 상단 버튼 패널
            _buttonPanel = new FlowLayoutPanel()
            {
                Dock = DockStyle.Top,
                Height = 60
            };

            // 버튼들 생성
            _btnAddRect = new Button { Text = "Add Rectangle", Width = 110 };
            _btnAddRect.Click += (s, e) => _drawingPanel.AddShape(ShapeType.Rectangle);

            _btnAddCircle = new Button { Text = "Add Circle", Width = 110 };
            _btnAddCircle.Click += (s, e) => _drawingPanel.AddShape(ShapeType.Circle);

            _btnAddEllipse = new Button { Text = "Add Ellipse", Width = 110 };
            _btnAddEllipse.Click += (s, e) => _drawingPanel.AddShape(ShapeType.Ellipse);

            _btnAddPolygonN = new Button { Text = "Add Polygon(N)", Width = 110 };
            _btnAddPolygonN.Click += (s, e) => _drawingPanel.AddPolygonWithInput();

            _btnDeleteShape = new Button { Text = "Delete Selected", Width = 110 };
            _btnDeleteShape.Click += (s, e) => _drawingPanel.DeleteSelectedShape();

            _btnZoomIn = new Button { Text = "Zoom In", Width = 80 };
            _btnZoomIn.Click += (s, e) => _drawingPanel.ZoomByButton(true);

            _btnZoomOut = new Button { Text = "Zoom Out", Width = 80 };
            _btnZoomOut.Click += (s, e) => _drawingPanel.ZoomByButton(false);

            // 버튼들을 패널에 넣기
            _buttonPanel.Controls.AddRange(new Control[]
            {
                _btnAddRect, _btnAddCircle, _btnAddEllipse,
                _btnAddPolygonN, _btnDeleteShape, _btnZoomIn, _btnZoomOut
            });

            // 커서 좌표 표시용 라벨 (하단)
            _lblCursorPos = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 25,
                Text = "Cursor: (0,0)"
            };

            // 실제 도형 편집용 패널
            _drawingPanel = new DrawingPanel(_lblCursorPos)
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            // 폼에 추가
            this.Controls.Add(_drawingPanel);
            this.Controls.Add(_lblCursorPos);
            this.Controls.Add(_buttonPanel);
        }
    }

    /// <summary>
    /// Canvas(패널) 역할을 하는 클래스
    /// </summary>
    public class DrawingPanel : Panel
    {
        private List<Shape> _shapes = new List<Shape>();
        private Shape _selectedShape;

        // 화면 이동(팬) 및 확대/축소
        private float _scale = 1.0f;
        private float _offsetX = 0f;
        private float _offsetY = 0f;

        // 마우스 드래그 상태
        private bool _isDraggingCanvas = false;
        private bool _isDraggingShape = false;
        private bool _isResizingShape = false;

        private ResizeHandleType _selectedHandle = ResizeHandleType.None;

        // 폴리곤 정점 편집 시, 몇 번째 정점을 드래그 중인지
        private int _draggingVertexIndex = -1;

        private PointF _lastMouseWorldPos;
        private Point _lastMouseScreenPos;

        private const float HANDLE_SIZE = 8f;

        // 커서 좌표 표시용 라벨
        private Label _lblCursorPos;

        public DrawingPanel(Label cursorLabel)
        {
            this.DoubleBuffered = true;
            this.MouseDown += DrawingPanel_MouseDown;
            this.MouseMove += DrawingPanel_MouseMove;
            this.MouseUp += DrawingPanel_MouseUp;
            this.MouseWheel += DrawingPanel_MouseWheel;

            _lblCursorPos = cursorLabel;
        }

        #region 도형 추가/삭제

        public void AddShape(ShapeType shapeType)
        {
            var shape = new Shape(shapeType);

            // 기본 위치/크기
            switch (shapeType)
            {
                case ShapeType.Rectangle:
                    shape.Bounds = new RectangleF(100, 100, 120, 80);
                    break;
                case ShapeType.Circle:
                    shape.Bounds = new RectangleF(300, 100, 80, 80);
                    break;
                case ShapeType.Ellipse:
                    shape.Bounds = new RectangleF(450, 100, 120, 80);
                    break;
                case ShapeType.Polygon:
                    // 예: 삼각형
                    shape.Points.AddRange(new[]
                    {
                        new PointF(600, 100),
                        new PointF(650, 180),
                        new PointF(550, 180)
                    });
                    shape.Bounds = GetPolygonBounds(shape.Points);
                    break;
            }
            _shapes.Add(shape);
            Invalidate();
        }

        /// <summary>
        /// N값을 입력받아 정N각형 그리기
        /// </summary>
        public void AddPolygonWithInput()
        {
            int n = PromptInteger("정N각형을 만들기 위해 N값(3이상)을 입력하세요", "Polygon Input", 5);
            if (n < 3) return;

            var shape = new Shape(ShapeType.Polygon);
            float centerX = 800;
            float centerY = 150;
            float radius = 50;

            for (int i = 0; i < n; i++)
            {
                double angle = (2 * Math.PI * i) / n - Math.PI / 2;
                float px = (float)(centerX + radius * Math.Cos(angle));
                float py = (float)(centerY + radius * Math.Sin(angle));
                shape.Points.Add(new PointF(px, py));
            }
            shape.Bounds = GetPolygonBounds(shape.Points);

            _shapes.Add(shape);
            Invalidate();
        }

        private int PromptInteger(string text, string caption, int defaultValue)
        {
            using (Form dlg = new Form())
            {
                dlg.Width = 300;
                dlg.Height = 150;
                dlg.Text = caption;

                Label lbl = new Label() { Text = text, Left = 10, Top = 10, Width = 260 };
                TextBox txt = new TextBox() { Left = 10, Top = 40, Width = 260, Text = defaultValue.ToString() };
                Button btnOk = new Button() { Text = "OK", Left = 100, Width = 80, Top = 70, DialogResult = DialogResult.OK };

                dlg.Controls.Add(lbl);
                dlg.Controls.Add(txt);
                dlg.Controls.Add(btnOk);
                dlg.AcceptButton = btnOk;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    if (int.TryParse(txt.Text, out int val))
                        return val;
                }
            }
            return defaultValue;
        }

        public void DeleteSelectedShape()
        {
            if (_selectedShape != null)
            {
                _shapes.Remove(_selectedShape);
                _selectedShape = null;
                Invalidate();
            }
        }

        #endregion

        #region Zoom (버튼 & 마우스 휠)

        public void ZoomByButton(bool zoomIn)
        {
            float factor = zoomIn ? 1.1f : 0.9f;
            float centerX = this.ClientSize.Width / 2f;
            float centerY = this.ClientSize.Height / 2f;

            ZoomAtScreenPoint(centerX, centerY, factor);
        }

        private void DrawingPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            float factor = (e.Delta > 0) ? 1.1f : 0.9f;
            ZoomAtScreenPoint(e.X, e.Y, factor);
        }

        private void ZoomAtScreenPoint(float screenX, float screenY, float factor)
        {
            PointF beforeWorld = ScreenToWorld(new PointF(screenX, screenY));

            _scale *= factor;
            if (_scale < 0.05f) _scale = 0.05f;
            if (_scale > 40f) _scale = 40f;

            PointF afterWorld = ScreenToWorld(new PointF(screenX, screenY));

            float dx = afterWorld.X - beforeWorld.X;
            float dy = afterWorld.Y - beforeWorld.Y;

            _offsetX += dx * _scale;
            _offsetY += dy * _scale;

            Invalidate();
        }

        #endregion

        #region 마우스 - 드래그 로직

        private void DrawingPanel_MouseDown(object sender, MouseEventArgs e)
        {
            PointF worldPos = ScreenToWorld(e.Location);

            if (e.Button == MouseButtons.Right ||
                (ModifierKeys == Keys.Control && e.Button == MouseButtons.Left))
            {
                // 우클릭(또는 Ctrl+좌클릭) -> 캔버스 이동
                _isDraggingCanvas = true;
                _lastMouseScreenPos = e.Location;
            }
            else if (e.Button == MouseButtons.Left)
            {
                // 1) 폴리곤 정점 핸들 클릭?
                var (poly, vertexIndex) = GetPolygonVertexAt(worldPos);
                if (poly != null && vertexIndex >= 0)
                {
                    _selectedShape = poly;
                    _selectedHandle = ResizeHandleType.PolygonVertex;
                    _draggingVertexIndex = vertexIndex;
                    _isResizingShape = true;
                    _lastMouseWorldPos = worldPos;
                    Invalidate();
                    return;
                }

                // 2) 사각형/원/타원 핸들(TL,TR,BL,BR)?
                if (_selectedShape != null)
                {
                    var ht = GetHandleAtScreenPoint(_selectedShape, e.Location);
                    if (ht != ResizeHandleType.None)
                    {
                        _selectedHandle = ht;
                        _isResizingShape = true;
                        _lastMouseWorldPos = worldPos;
                        return;
                    }
                }

                // 3) 일반 도형 선택
                _selectedShape = GetShapeAt(worldPos);

                // Ctrl+좌클릭 -> 도형 복제
                if (_selectedShape != null && ModifierKeys == Keys.Control)
                {
                    _selectedShape = DuplicateShape(_selectedShape);
                }

                if (_selectedShape != null)
                {
                    _isDraggingShape = true;
                    _lastMouseWorldPos = worldPos;
                }
                else
                {
                    // 선택 해제
                    _isResizingShape = false;
                    _selectedHandle = ResizeHandleType.None;
                }

                Invalidate();
            }
        }

        private void DrawingPanel_MouseMove(object sender, MouseEventArgs e)
        {
            // 커서 좌표 업데이트
            PointF worldPos = ScreenToWorld(e.Location);
            _lblCursorPos.Text = $"Cursor: ({worldPos.X:F1}, {worldPos.Y:F1})";

            if (_isDraggingCanvas)
            {
                float dx = e.X - _lastMouseScreenPos.X;
                float dy = e.Y - _lastMouseScreenPos.Y;
                _offsetX += dx;
                _offsetY += dy;
                _lastMouseScreenPos = e.Location;
                Invalidate();
            }
            else if (_isResizingShape && _selectedShape != null)
            {
                float dx = worldPos.X - _lastMouseWorldPos.X;
                float dy = worldPos.Y - _lastMouseWorldPos.Y;

                if (_selectedHandle == ResizeHandleType.PolygonVertex && _draggingVertexIndex >= 0)
                {
                    // 폴리곤 정점 이동
                    MovePolygonVertex(_selectedShape, _draggingVertexIndex, dx, dy);
                }
                else
                {
                    // 사각형/원/타원 리사이즈
                    ResizeShape(_selectedShape, dx, dy, _selectedHandle);
                }

                _lastMouseWorldPos = worldPos;
                Invalidate();
            }
            else if (_isDraggingShape && _selectedShape != null)
            {
                float dx = worldPos.X - _lastMouseWorldPos.X;
                float dy = worldPos.Y - _lastMouseWorldPos.Y;
                MoveShape(_selectedShape, dx, dy);
                _lastMouseWorldPos = worldPos;
                Invalidate();
            }
        }

        private void DrawingPanel_MouseUp(object sender, MouseEventArgs e)
        {
            _isDraggingCanvas = false;
            _isDraggingShape = false;
            _isResizingShape = false;
            _selectedHandle = ResizeHandleType.None;
            _draggingVertexIndex = -1;
        }

        #endregion

        #region OnPaint (그리기)

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // 1) Grid 표시: "패널 크기를 일정 분할"하여 그리드
            DrawGrid(e.Graphics);

            // 2) 변환 적용
            e.Graphics.TranslateTransform(_offsetX, _offsetY);
            e.Graphics.ScaleTransform(_scale, _scale);

            // 3) 도형 그리기
            foreach (var shape in _shapes)
            {
                using (Pen pen = new Pen(shape.Color, 2f))
                {
                    if (shape == _selectedShape)
                    {
                        pen.Color = Color.Red;
                        pen.Width = 3f;
                    }

                    switch (shape.ShapeType)
                    {
                        case ShapeType.Rectangle:
                            e.Graphics.DrawRectangle(
                                pen,
                                shape.Bounds.X,
                                shape.Bounds.Y,
                                shape.Bounds.Width,
                                shape.Bounds.Height);
                            break;
                        case ShapeType.Circle:
                            e.Graphics.DrawEllipse(
                                pen,
                                shape.Bounds.X,
                                shape.Bounds.Y,
                                shape.Bounds.Width,
                                shape.Bounds.Height);
                            break;
                        case ShapeType.Ellipse:
                            e.Graphics.DrawEllipse(
                                pen,
                                shape.Bounds.X,
                                shape.Bounds.Y,
                                shape.Bounds.Width,
                                shape.Bounds.Height);
                            break;
                        case ShapeType.Polygon:
                            if (shape.Points.Count > 2)
                                e.Graphics.DrawPolygon(pen, shape.Points.ToArray());
                            break;
                    }

                    // 도형 정보 표시
                    string infoText = $"(X={shape.Bounds.X:F1}, Y={shape.Bounds.Y:F1}, W={shape.Bounds.Width:F1}, H={shape.Bounds.Height:F1})";
                    PointF textPos = new PointF(shape.Bounds.X, shape.Bounds.Y - 15);
                    e.Graphics.DrawString(infoText, this.Font, Brushes.Black, textPos);
                }
            }

            // 4) 선택된 도형의 핸들(모서리/폴리곤 정점) 그리기
            if (_selectedShape != null)
            {
                DrawHandles(e.Graphics, _selectedShape);
            }
        }

        /// <summary>
        /// 패널(ClientSize) 기준으로 일정 분할 그리드 표시
        /// </summary>
        private void DrawGrid(Graphics g)
        {
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;

            // 예: 가로 10분할, 세로 10분할
            int gridDivX = 10;
            int gridDivY = 10;

            float stepX = (float)w / gridDivX;
            float stepY = (float)h / gridDivY;

            using (var pen = new Pen(Color.LightGray, 1f))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

                // 세로선
                for (int i = 1; i < gridDivX; i++)
                {
                    float x = stepX * i;
                    g.DrawLine(pen, x, 0, x, h);
                }
                // 가로선
                for (int j = 1; j < gridDivY; j++)
                {
                    float y = stepY * j;
                    g.DrawLine(pen, 0, y, w, y);
                }
            }
        }

        #endregion

        #region 도형/버텍스 충돌, 이동, 복제, 리사이즈

        private Shape GetShapeAt(PointF worldPos)
        {
            for (int i = _shapes.Count - 1; i >= 0; i--)
            {
                var s = _shapes[i];
                switch (s.ShapeType)
                {
                    case ShapeType.Rectangle:
                    case ShapeType.Circle:
                    case ShapeType.Ellipse:
                        if (s.Bounds.Contains(worldPos))
                            return s;
                        break;
                    case ShapeType.Polygon:
                        if (IsPointInPolygon(worldPos, s.Points))
                            return s;
                        break;
                }
            }
            return null;
        }

        /// <summary>
        /// 폴리곤 정점(버텍스)에 대한 HitTest
        /// </summary>
        private (Shape polygon, int vertexIndex) GetPolygonVertexAt(PointF worldPos)
        {
            for (int i = _shapes.Count - 1; i >= 0; i--)
            {
                var shape = _shapes[i];
                if (shape.ShapeType == ShapeType.Polygon)
                {
                    for (int v = 0; v < shape.Points.Count; v++)
                    {
                        RectangleF vertexRect = new RectangleF(
                            shape.Points[v].X - HANDLE_SIZE / 2,
                            shape.Points[v].Y - HANDLE_SIZE / 2,
                            HANDLE_SIZE,
                            HANDLE_SIZE);

                        if (vertexRect.Contains(worldPos))
                        {
                            return (shape, v);
                        }
                    }
                }
            }
            return (null, -1);
        }

        private bool IsPointInPolygon(PointF pt, List<PointF> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                bool intersect = ((polygon[i].Y > pt.Y) != (polygon[j].Y > pt.Y)) &&
                    (pt.X < (polygon[j].X - polygon[i].X) * (pt.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X);
                if (intersect) inside = !inside;
            }
            return inside;
        }

        private void MovePolygonVertex(Shape polygon, int vIndex, float dx, float dy)
        {
            polygon.Points[vIndex] = new PointF(
                polygon.Points[vIndex].X + dx,
                polygon.Points[vIndex].Y + dy);

            polygon.Bounds = GetPolygonBounds(polygon.Points);
        }

        private Shape DuplicateShape(Shape original)
        {
            var newShape = new Shape(original.ShapeType);
            newShape.Color = original.Color;

            if (original.ShapeType == ShapeType.Polygon)
            {
                newShape.Points = original.Points.Select(p => new PointF(p.X + 20, p.Y + 20)).ToList();
                newShape.Bounds = GetPolygonBounds(newShape.Points);
            }
            else
            {
                newShape.Bounds = new RectangleF(
                    original.Bounds.X + 20,
                    original.Bounds.Y + 20,
                    original.Bounds.Width,
                    original.Bounds.Height);
            }

            _shapes.Add(newShape);
            return newShape;
        }

        private void MoveShape(Shape shape, float dx, float dy)
        {
            if (shape.ShapeType == ShapeType.Polygon)
            {
                for (int i = 0; i < shape.Points.Count; i++)
                {
                    shape.Points[i] = new PointF(shape.Points[i].X + dx, shape.Points[i].Y + dy);
                }
                shape.Bounds = GetPolygonBounds(shape.Points);
            }
            else
            {
                shape.Bounds = new RectangleF(
                    shape.Bounds.X + dx,
                    shape.Bounds.Y + dy,
                    shape.Bounds.Width,
                    shape.Bounds.Height);
            }
        }

        /// <summary>
        /// 폴리곤 Bounds 계산
        /// </summary>
        private RectangleF GetPolygonBounds(List<PointF> pts)
        {
            if (pts == null || pts.Count == 0) return RectangleF.Empty;

            float minX = pts.Min(p => p.X);
            float maxX = pts.Max(p => p.X);
            float minY = pts.Min(p => p.Y);
            float maxY = pts.Max(p => p.Y);

            return RectangleF.FromLTRB(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// 사각형/원/타원 리사이즈 (Circle은 항상 정원 유지)
        /// 좌표가 뒤집혔을 경우 swap
        /// </summary>
        private void ResizeShape(Shape shape, float dx, float dy, ResizeHandleType handle)
        {
            // Polygon은 정점 별도로 처리
            if (shape.ShapeType == ShapeType.Polygon)
                return;

            RectangleF b = shape.Bounds;
            float newLeft = b.Left;
            float newTop = b.Top;
            float newRight = b.Right;
            float newBottom = b.Bottom;

            switch (handle)
            {
                case ResizeHandleType.TopLeft:
                    newLeft += dx;
                    newTop += dy;
                    break;
                case ResizeHandleType.TopRight:
                    newRight += dx;
                    newTop += dy;
                    break;
                case ResizeHandleType.BottomLeft:
                    newLeft += dx;
                    newBottom += dy;
                    break;
                case ResizeHandleType.BottomRight:
                    newRight += dx;
                    newBottom += dy;
                    break;
            }

            // 최소 크기
            float minSize = 5f;
            if ((newRight - newLeft) < minSize) newRight = newLeft + minSize;
            if ((newBottom - newTop) < minSize) newBottom = newTop + minSize;

            // 좌우/상하가 뒤집혔을 경우 swap
            if (newLeft > newRight)
            {
                float temp = newLeft;
                newLeft = newRight;
                newRight = temp;
            }
            if (newTop > newBottom)
            {
                float temp = newTop;
                newTop = newBottom;
                newBottom = temp;
            }

            // Circle이면 정사각형 유지
            if (shape.ShapeType == ShapeType.Circle)
            {
                float width = newRight - newLeft;
                float height = newBottom - newTop;
                float size = Math.Min(width, height);

                switch (handle)
                {
                    case ResizeHandleType.TopLeft:
                        newLeft = b.Right - size;
                        newTop = b.Bottom - size;
                        break;
                    case ResizeHandleType.TopRight:
                        newRight = b.Left + size;
                        newTop = b.Bottom - size;
                        break;
                    case ResizeHandleType.BottomLeft:
                        newLeft = b.Right - size;
                        newBottom = b.Top + size;
                        break;
                    case ResizeHandleType.BottomRight:
                        newRight = b.Left + size;
                        newBottom = b.Top + size;
                        break;
                }

                // 다시 swap 검사 (원형 고정 시 뒤집힐 수도 있음)
                if (newLeft > newRight)
                {
                    float temp = newLeft;
                    newLeft = newRight;
                    newRight = temp;
                }
                if (newTop > newBottom)
                {
                    float temp = newTop;
                    newTop = newBottom;
                    newBottom = temp;
                }
            }

            shape.Bounds = RectangleF.FromLTRB(newLeft, newTop, newRight, newBottom);
        }

        #endregion

        #region 핸들 그리기

        /// <summary>
        /// 선택된 도형의 핸들(Polygon 정점, Bounds 4모서리 등) 그리기
        /// </summary>
        private void DrawHandles(Graphics g, Shape shape)
        {
            using (var pen = new Pen(Color.Blue, 1f))
            {
                // [A] Polygon이면 각 버텍스에 핸들
                if (shape.ShapeType == ShapeType.Polygon)
                {
                    foreach (var pt in shape.Points)
                    {
                        RectangleF rF = new RectangleF(
                            pt.X - HANDLE_SIZE / 2,
                            pt.Y - HANDLE_SIZE / 2,
                            HANDLE_SIZE,
                            HANDLE_SIZE);

                        g.FillRectangle(Brushes.White, rF);
                        g.DrawRectangle(pen, rF.X, rF.Y, rF.Width, rF.Height);
                    }
                }

                // [B] 공통: 바운딩 박스 점선
                var dashPen = new Pen(Color.Blue, 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
                g.DrawRectangle(dashPen,
                    shape.Bounds.X, shape.Bounds.Y,
                    shape.Bounds.Width, shape.Bounds.Height);

                // [C] 사각형/원/타원 -> 4 모서리 핸들
                if (shape.ShapeType != ShapeType.Polygon)
                {
                    var handles = GetHandleRectangles(shape.Bounds);
                    foreach (var kvp in handles)
                    {
                        RectangleF rF = kvp.Value;
                        g.FillRectangle(Brushes.White, rF);
                        g.DrawRectangle(pen, rF.X, rF.Y, rF.Width, rF.Height);
                    }
                }
            }
        }

        /// <summary>
        /// Bounds 4모서리에 대한 핸들 Rect
        /// </summary>
        private Dictionary<ResizeHandleType, RectangleF> GetHandleRectangles(RectangleF b)
        {
            var dict = new Dictionary<ResizeHandleType, RectangleF>();

            float xMin = b.Left;
            float xMax = b.Right;
            float yMin = b.Top;
            float yMax = b.Bottom;

            dict[ResizeHandleType.TopLeft] = new RectangleF(xMin - HANDLE_SIZE / 2, yMin - HANDLE_SIZE / 2, HANDLE_SIZE, HANDLE_SIZE);
            dict[ResizeHandleType.TopRight] = new RectangleF(xMax - HANDLE_SIZE / 2, yMin - HANDLE_SIZE / 2, HANDLE_SIZE, HANDLE_SIZE);
            dict[ResizeHandleType.BottomLeft] = new RectangleF(xMin - HANDLE_SIZE / 2, yMax - HANDLE_SIZE / 2, HANDLE_SIZE, HANDLE_SIZE);
            dict[ResizeHandleType.BottomRight] = new RectangleF(xMax - HANDLE_SIZE / 2, yMax - HANDLE_SIZE / 2, HANDLE_SIZE, HANDLE_SIZE);

            return dict;
        }

        private ResizeHandleType GetHandleAtScreenPoint(Shape shape, Point screenPt)
        {
            PointF worldPos = ScreenToWorld(screenPt);
            var handles = GetHandleRectangles(shape.Bounds);

            foreach (var kvp in handles)
            {
                if (kvp.Value.Contains(worldPos))
                {
                    return kvp.Key;
                }
            }
            return ResizeHandleType.None;
        }

        #endregion

        #region 좌표 변환

        private PointF ScreenToWorld(PointF screenPos)
        {
            float worldX = (screenPos.X - _offsetX) / _scale;
            float worldY = (screenPos.Y - _offsetY) / _scale;
            return new PointF(worldX, worldY);
        }

        #endregion
    }
}
