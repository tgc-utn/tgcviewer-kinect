
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows;
using Microsoft.DirectX.Direct3D;
using Microsoft.DirectX;
using System.Drawing;
using TgcViewer.Utils.TgcSceneLoader;
using TgcViewer.Utils.Input;



namespace TgcViewer.Utils.Gui
{
    public enum tipoCursor
    {
        sin_cursor,
        targeting,
        over,
        progress,
        pressed,
        gripped
    }

    public enum itemState
    {
        normal,
        hover,
        pressed,
        disabled
    }

    
    public enum MessageType
    {
        WM_NOTHING,
        WM_PRESSING,
        WM_COMMAND,
    }


    public struct GuiMessage
    {
        public MessageType message;
        public int id;
    }


    // Vertex format para dibujar en 2d 
    public struct VERTEX2D
    {
        public float x,y,z,rhw;		// Posicion
        public int color;		// Color
    };


    public struct st_dialog
    {
        public int item_0;
        public bool trapezoidal_style;
        public bool autohide;
    };




    public class DXGui
    {
        // Defines
        public const int MAX_GUI_ITEMS = 100;
        public const int MAX_TEXTURAS = 50;
        public const int MAX_CURSOR = 10;
        public const int MAX_DIALOG = 20;
        // Eventos
        public const int EVENT_FIRST_SCROLL = 60000;
        public const int EVENT_SCROLL_LEFT = 60000;
        public const int EVENT_SCROLL_RIGHT = 60001;
        // Otras
        public float M_PI = (float)Math.PI;
        public float M_PI_2 = (float)Math.PI * 0.5f;


        public gui_item[] items = new gui_item[MAX_GUI_ITEMS];
		public int cant_items;
		public int item_0;
		public int sel;		            // item seleccionado
        public int item_pressed;		// item prsionado

		public int foco;		        // item con foco
        public float delay_sel;
        public float delay_sel0;
        public float delay_press;
        public float delay_press0;
        public float time;
        public float delay_show;
        public bool hidden;
        public Matrix RTQ;              // Matriz de cambio de Rectangle to Quad (Trapezoidal quad)

        // Estilos del dialogo actual
        public bool trapezoidal_style;
        public bool autohide;
        // Colores x defecto
        public static Color c_fondo = Color.FromArgb(80, 30, 155, 110);        // Color de fondo 
        public static Color c_font = Color.FromArgb(255, 255, 255);            // Color de font item normal
        public static Color c_selected = Color.FromArgb(255, 128, 128);        // Color de font item seleccionado
        public static Color c_selected_frame = Color.FromArgb(128, 192, 255);  // Color de borde selected rect
        public static Color c_grad_inf_0 = Color.FromArgb(255, 254, 237);       // Color gradiente inferior menu item valor hasta
        public static Color c_grad_inf_1 = Color.FromArgb(255, 235, 182);       // Color gradiente inferior menu item valor desde 
        public static Color c_grad_sup_0 = Color.FromArgb(255, 231, 162);       // Color gradiente superior menu item valor hasta
        public static Color c_grad_sup_1 = Color.FromArgb(255, 217, 120);       // Color gradiente superior menu item valor desde 
        public static Color c_buttom_frame = Color.FromArgb(80, 220, 20);       // Color recuadro del boton 
        public static Color c_buttom_selected = Color.FromArgb(184, 224, 248);  // Color interior del boton seleccionado
        public static Color c_buttom_text = Color.FromArgb(130, 255, 130);      // Color texto del boton
        public static Color c_buttom_sel_text = Color.FromArgb(255, 220, 220);  // Color texto del boton seleccionado
        public static Color c_frame_border = Color.FromArgb(130, 255, 130);     // Color borde los frames

        
        // Cableados
		public int rbt;				    // radio button transfer
		public Color sel_color;		    // color seleccionado

		// Escala y origen global de todo el dialogo
        public float ex,ey,ox,oy;
        // origen de los items scrolleables
        public float sox, soy;

        // pila para dialogos
		public st_dialog []dialog= new st_dialog[MAX_DIALOG];		// pila para guardar el primer item
        public int cant_dialog;

		public Sprite sprite;
		public Line line;
		public Microsoft.DirectX.Direct3D.Font font;

		// Cursores
        public Texture[] cursores = new Texture[MAX_CURSOR];
        public tipoCursor cursor_der, cursor_izq;

        // Posicion del mouse
        public float mouse_x;
        public float mouse_y;

        // Input de la kinect
        public kinect_input kinect = new kinect_input();

        // Camara TGC
        public FocusCamera camera;


        public DXGui()
        {
        	cant_items = 0;
	        cant_dialog = 0;
            trapezoidal_style = true;
            autohide = false;

            // Computo la matrix de cambio rect to quad
            float W = GuiController.Instance.Panel3d.Width;
            float H = GuiController.Instance.Panel3d.Height;
            RTQ = rectToQuad(0, 0, W, H,0, 0, W-50, 60, W-100, H-50,0, H);

        }

        public void Reset()
        {
	        cant_items = 0;
            item_pressed = sel = -1;
	        time = 0;
	        item_0 = 0;
	        ey = ex = 1;
	        ox = oy = 0;
            sox = soy = 0;
            mouse_x = mouse_y = -1;
            for (int i = 0; i < MAX_CURSOR; ++i)
                cursores[i] = null;

            cursor_izq = tipoCursor.sin_cursor;
            cursor_der = tipoCursor.targeting;
            
        }

        public void Dispose()
        {
            font.Dispose();
            sprite.Dispose();
            line.Dispose();
            for (int i = 0; i < MAX_CURSOR; ++i)
                if (cursores[i] != null)
                    cursores[i].Dispose();
        }

		// interface
        public void Create()
        {
	        Reset();
            // Creo el sprite
            Device d3dDevice = GuiController.Instance.D3dDevice;
            sprite = new Sprite(d3dDevice);
            // lines varios
            line = new Line(d3dDevice);
            // Fonts
            font = new Microsoft.DirectX.Direct3D.Font(d3dDevice, 22, 0, FontWeight.Light, 0, false, CharacterSet.Default,
                    Precision.Default, FontQuality.Default, PitchAndFamily.DefaultPitch, "Lucida Console");
            font.PreloadGlyphs('0', '9');
            font.PreloadGlyphs('a', 'z');
            font.PreloadGlyphs('A', 'Z');

            // Cargo las textura del cursor
            cursores[(int)tipoCursor.targeting] = cargar_textura("cursor_default.png", true);
            cursores[(int)tipoCursor.over] = cargar_textura("cursor_over.png", true);
            cursores[(int)tipoCursor.gripped] = cargar_textura("cursor_gripper.png", true);

        }

        // dialog support
        public void InitDialog(bool pautohide=false,bool trapezoidal = true)
        {
	        // guardo el valor de item_0 en la pila
	        dialog[cant_dialog].item_0 = item_0;
            // y el valor del estilo del dialogo actual
            dialog[cant_dialog].trapezoidal_style = trapezoidal_style;
            dialog[cant_dialog].autohide = autohide;
            ++cant_dialog;
	        // y el primer item del nuevo dialog es cant items
	        item_0 = cant_items;
            // y seteo el nuevo estilo de dialogo
            trapezoidal_style = trapezoidal;
            autohide = pautohide;
            foco = -1;
	        rbt = -1;
	        sel = -1;
            Show();

        }

        public void EndDialog()
        {
	        // actualizo la cantidad de items
	        cant_items = item_0;
	        // recupero el valor de item_0 y del estilo del dialogo
            --cant_dialog;
	        item_0 = dialog[cant_dialog].item_0;
            trapezoidal_style = dialog[cant_dialog].trapezoidal_style;
            autohide = dialog[cant_dialog].autohide;
            // Saco el foco
	        foco = -1;
	        // valores x defecto
	        ey = ex = 1;
	        ox = oy = 0;
            sox = soy = 0;
        }


        public void Show(bool show=true)
        {
            hidden = !show;
            delay_show = autohide?1:0;
        }
		
        // Alerts 
        public void MessageBox(string msg,string titulo="")
        {
            InitDialog(false, false);
            float W = GuiController.Instance.Panel3d.Width;
            float H = GuiController.Instance.Panel3d.Height;

            int dx = 600;
            int dy = 400;
            int x0 = (int)((W-dx) / 2);
            int y0 = (int)((H-dy) / 2);

            InsertFrame(titulo, x0, y0, dx, dy, Color.FromArgb(64,32,64));
            
            InsertItem(msg, x0+100, y0+100);
            InsertKinectCircleButton(0, "OK", "ok.png", x0 + dx / 4, y0 + dy - 130, 30);
            InsertKinectCircleButton(1, "CANCEL", "cancel.png", x0 + 3 * dx / 4, y0 + dy - 130, 30);
        }

		// input
        public GuiMessage ProcessInput()
        {
            GuiMessage msg = new GuiMessage();
            msg.message = MessageType.WM_NOTHING;
            msg.id = -1;
	        int ant_sel = sel;

            // Tomo el input de la kinect
            kinect.GetInputFromMouse();
            // Reconozco el gesto
            kinect.GestureRecognition();

            // Simulo que movio el mouse
            st_hand hand = kinect.right_hand_sel? kinect.right_hand : kinect.left_hand;
            float sx = hand.position.X;
            float sy = hand.position.Y;

            // Autohide dialog
            if (autohide)
            {
                if (mouse_x < 10 && hidden)
                    // El dialogo esta oculto y se mueve con el mouse a posicion izquierda
                    Show();
                else
                if (mouse_x > 400 && !hidden)
                    // El dialogo esta visible y se mueve con el mouse a posicion derecha
                    Show(false);
            }


            //if(mouse_x!=sx || mouse_y!=sy)
                // mouse move...


            if (kinect.right_hand_sel && kinect.right_hand.gripping)
            {
                // mano derecha cerrada
                cursor_der = tipoCursor.gripped;
                // scroll (PAN) de los items scrolleables
                sox -= mouse_x - sx;
                soy -= mouse_y - sy;
            }
            else
            {
                // verifico si el cusor pasa por arriba de un item, si es seleccionable, lo muestro
                sel = -1;
                int t = item_0;
                while (t < cant_items && sel == -1)
                {
                    if (items[t].seleccionable || items[t].auto_seleccionable)
                    {
                        Point pt = new Point(0,0);
                        if (items[t].scrolleable)
                        {
                            pt.X = (int)(sx - sox);
                            pt.Y = (int)(sy - soy);
                        }
                        else
                        {
                            pt.X = (int)sx;
                            pt.Y = (int)sy;
                        }
                        if (items[t].pt_inside(this, pt))
                            sel = t;
                    }
                    ++t;
                }

                if (kinect.right_hand_sel)
                    cursor_der = sel != -1 ? tipoCursor.over : tipoCursor.targeting;
                else
                    cursor_izq = sel != -1 ? tipoCursor.over : kinect.left_hand.gripping ? tipoCursor.gripped : tipoCursor.targeting;
            }

            // Caso particular, items autoseleccionables, quiero que se genere el evento cuando pasa la mano por el control
            // De momento soporta el auto scroll
            if (sel != -1 && items[sel].auto_seleccionable)
            {
                switch (items[sel].item_id)
                {
                    case EVENT_SCROLL_LEFT:
                        sox -= 1;
                        break;
                    case EVENT_SCROLL_RIGHT:
                        sox += 1;
                        break;
                }

                // anulo el resgo de los eventos para este item
                sel = -1;
            }

            if (ant_sel != sel)
            {
                // cambio de seleccion
                if (ant_sel != -1)
                    items[ant_sel].state = itemState.normal;
                if (sel != -1)
                    items[sel].state = itemState.hover;

                // inicio el timer de seleccion
                delay_sel0 = delay_sel = 0.5f;
            }

            switch(kinect.currentGesture)
            {
                case Gesture.Nothing:
                default:
                    break;

                case Gesture.Pressing:
                    // Presiona el item actual
                    if (sel != -1)
                    {
                        items[sel].state = itemState.pressed;
                        // inicio el timer de press
                        delay_press0 = delay_press = 0.5f;
                        // genero el mensaje
                        msg.message = MessageType.WM_PRESSING;
                        msg.id = items[sel].item_id;
                        // guardo el item presionado, por si se mueve del mismo antes que se genere el evento wm_command
                        item_pressed = sel;
                    }
                    break;
            }

            // Actualizo la pos del mouse
            mouse_x = sx;
            mouse_y = sy;

            return msg;
        }

        public GuiMessage Update(float elapsed_time)
        {
            // Actualizo los timers
            time += elapsed_time;

            if (delay_show > 0)
            {
                delay_show -= elapsed_time;
                if (delay_show < 0)
                    delay_show  = 0;

                if(hidden)
                    ox = -200 * (2-delay_show);
                else
                    ox = -200 * delay_show;
            }

            if (delay_sel > 0)
            {
                delay_sel -= elapsed_time;
                if (delay_sel < 0)
                    delay_sel = 0;
            }

            // computo la matriz de transformacion final RTQ 
            if (trapezoidal_style)
            {
                float W = GuiController.Instance.Panel3d.Width;
                float H = GuiController.Instance.Panel3d.Height;
                RTQ = rectToQuad(0, 0, W, H,
                              0, 0, W - 50, 60, W - 100, H - 50, 0, H);
            }
            else
                RTQ = Matrix.Identity;

            if (delay_press > 0)
            {
                delay_press -= elapsed_time;
                if (delay_press < 0)
                {
                    // Termino el delay de press 
                    delay_press = 0;
                    // Si habia algun item presionado, lo libero
                    if (sel != -1 && items[sel].state == itemState.pressed)
                        items[sel].state = itemState.normal;

                    // Aca es el mejor momento para generar el msg, asi el usuario tiene tiempo de ver la animacion
                    // de que el boton se esta presionando, antes que se triggere el comando
                    // genero el mensaje, ojo, uso item_pressed, porque por ahi se movio desde el momento que se genero
                    // el primer evento de pressing y en ese caso sel!=item_pressed
                    if (item_pressed != -1)
                    {
                        GuiMessage msg = new GuiMessage();
                        msg.message = MessageType.WM_COMMAND;
                        msg.id = items[item_pressed].item_id;
                        // Y limpio el item pressed, evitando cualquier posibilidad de generar 2 veces el mismo msg
                        item_pressed = -1;
                        return msg;     // y termino de procesar por este frame
                    }
                }

            }

            // Actualizo el timer de los items actuales
            for (int i = item_0; i < cant_items; ++i)
                items[i].ftime += elapsed_time;

            // Proceso el input y devuelve el resultado
            return ProcessInput();
        }

		public void Render()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            // elimino cualquier textura que me cague el modulate del vertex color
            d3dDevice.SetTexture(0, null);
            // Desactivo el zbuffer
            bool ant_zenable = d3dDevice.RenderState.ZBufferEnable;
            d3dDevice.RenderState.ZBufferEnable = false;

            // 1- dibujo los items 2d con una interface de sprites

            sprite.Begin(SpriteFlags.AlphaBlend);
            Matrix matAnt = sprite.Transform * Matrix.Identity;
            Vector2 scale = new Vector2(ex,ey);
            Vector2 offset = new Vector2(ox,oy);
            sprite.Transform = Matrix.Transformation2D(new Vector2(0, 0), 0, scale, new Vector2(0, 0), 0, offset) * RTQ;
            
            for (int i = item_0; i < cant_items; ++i)
                    if(!items[i].item3d)
                        items[i].Render(this);

            // 2 - dibujo el cusor con la misma interface de prites
            sprite.Transform = Matrix.Transformation2D(new Vector2(0, 0), 0, scale, new Vector2(0, 0), 0, new Vector2(0, 0));

            Vector2 scale_center = new Vector2(kinect.left_hand.position.X, kinect.left_hand.position.Y);
            // mano derecha
            if (kinect.right_hand.visible && cursores[(int)cursor_der]!=null)
            {
                sprite.Transform = Matrix.Transformation2D(scale_center, 0, scale, Vector2.Empty, 0, new Vector2(0, 0));
                sprite.Draw(cursores[(int)cursor_der], Rectangle.Empty, new Vector3(32, 32, 0), kinect.right_hand.position, Color.FromArgb(255, 255, 255, 255));
            }
            // mano izquierda
            if (kinect.left_hand.visible && cursores[(int)cursor_izq]!=null)
            {
                // dibujo espejado
                scale.X *= -1;
                sprite.Transform = Matrix.Transformation2D(scale_center, 0, scale, Vector2.Empty, 0, new Vector2(0, 0));
                sprite.Draw(cursores[(int)cursor_izq], Rectangle.Empty, new Vector3(32, 32, 0), kinect.left_hand.position, Color.FromArgb(255, 255, 255, 255));
            }
            // Restauro la transformacion del sprite
            sprite.Transform = matAnt;
            sprite.End();


            // 3- dibujo los items 3d a travez de la interface usual del TGC (usando la camara y un viewport)
            d3dDevice.RenderState.ZBufferEnable = true;
            for (int i = item_0; i < cant_items; ++i)
                    if (items[i].item3d)
                        items[i].Render(this);

            d3dDevice.RenderState.ZBufferEnable = ant_zenable;


        }



        // Interface para agregar items al UI
        public gui_item InsertItem(gui_item item)
        {
            // Inserto el gui item
            items[cant_items] = item;
            // Devuelvo el item pp dicho agregado a la lista
            return items[cant_items++];
        }

        // Inserta un item generico 
        public gui_item InsertItem(String s, int x, int y, int dx=0, int dy=0)
        {
            // Static text = item generico
            return InsertItem(new gui_item(this, s, x, y, dx, dy));
        }

        // Pop up menu item
        public gui_menu_item InsertMenuItem(int id,String s, int x, int y, int dx = 0, int dy = 0)
        {
            return (gui_menu_item)InsertItem(new gui_menu_item(this, s,id, x, y, dx, dy));
        }

        // Standard push button
        public gui_button InsertButton(int id, String s, int x, int y, int dx, int dy)
        {
            return (gui_button)InsertItem(new gui_button(this, s,id, x, y, dx, dy));
        }

        // kinect button
        public gui_kinect_circle_button InsertKinectCircleButton(int id, String s, String imagen, int x, int y, int r)
        {
            return (gui_kinect_circle_button)InsertItem(new gui_kinect_circle_button(this, s, imagen, id, x, y, r));
        }
        public gui_kinect_tile_button InsertKinectTileButton(int id, String s, String imagen, int x, int y, int dx,int dy,bool scrolleable=true)
        {
            return (gui_kinect_tile_button)InsertItem(new gui_kinect_tile_button(this, s, imagen, id, x, y, dx,dy,scrolleable));
        }
        public gui_kinect_scroll_button InsertKinectScrollButton(int tscroll, String imagen, int x, int y, int dx, int dy)
        {
            return (gui_kinect_scroll_button)InsertItem(new gui_kinect_scroll_button(this, imagen, tscroll, x, y, dx, dy));
        }

        // Dialog Frame 
        public gui_frame InsertFrame(String s, int x, int y, int dx, int dy,Color c_fondo)
        {
            return (gui_frame)InsertItem(new gui_frame(this, s, x, y, dx, dy, c_fondo));
        }

        // mesh buttons
        public gui_mesh_button InsertMeshButton(int id, String s,String fname, int x, int y, int dx, int dy)
        {
            return (gui_mesh_button)InsertItem(new gui_mesh_button(this, s,fname,id, x, y, dx, dy));
        }

         
		// line support
		public void Transform(VERTEX2D []pt,int cant_ptos)
        {
	        for(int i=0;i<cant_ptos;++i)
	        {
                float x = ox + pt[i].x*ex;
                float y = oy + pt[i].y*ey;

                pt[i].x = x * RTQ.M11 + y * RTQ.M21 + RTQ.M41;
                pt[i].y = x * RTQ.M12 + y * RTQ.M22 + RTQ.M42;
                float W = x * RTQ.M14 + y * RTQ.M24 + RTQ.M44;

                pt[i].x /= W;
                pt[i].y /= W;


	        }
        }

		public void Transform(Vector2 []pt,int cant_ptos)
        {
	        for(int i=0;i<cant_ptos;++i)
	        {
                float x = ox + pt[i].X * ex;
                float y = oy + pt[i].Y * ey;
                pt[i].X = x * RTQ.M11 + y * RTQ.M21 + RTQ.M41;
                pt[i].Y = x * RTQ.M12 + y * RTQ.M22 + RTQ.M42;
                float W = x * RTQ.M14 + y * RTQ.M24 + RTQ.M44;

                pt[i].X /= W;
                pt[i].Y /= W;




	        }
        }

        public void DrawPoly(Vector2 []V,int cant_ptos,int dw,Color color)
        {
	        if(dw<1)
		        dw = 1;
	        // Elimino ptos repetidos
	        Vector2 []P = new Vector2 [1000];
	        int cant = 1;
	        P[0] = V[0];
	        for(int i=1;i<cant_ptos;++i)
		        if((V[i]-V[i-1]).Length()>0.01)
			        P[cant++] = V[i];

	        cant_ptos = cant;
	        bool closed  = (P[0]-P[cant_ptos-1]).Length()<0.1;
	
	        // calculo el offset
	        Vector2 []Q = new Vector2 [1000];
	        Vector2 []N = new Vector2 [1000];
	        for(int i=0;i<cant_ptos-1;++i)
	        {
		        Vector2 p0 = P[i];
		        Vector2 p1 = P[i+1];
		        Vector2 v = p1-p0;
		        v.Normalize();
                // N = V.normal()
		        N[i].X = -v.Y;
		        N[i].Y = v.X;
	        }

	        // ptos intermedios
	        int i0 = closed?0:1;
	        for(int i=i0;i<cant_ptos;++i)
	        {
		        int ia = i!=0?i-1:cant_ptos-2;
		        Vector2 n = N[ia]+N[i];
		        n.Normalize();
		        float r = Vector2.Dot(N[ia],n);
		        if(r!=0)
			        Q[i] = P[i] + n*((float)dw/r);
		        else
			        Q[i] = P[i];

	        }

	        if(!closed)
	        {
		        // poligono abierto: primer y ultimo punto: 
		        Q[0] = P[0] + N[0]*dw;
		        Q[cant_ptos-1] = P[cant_ptos-1] + N[cant_ptos-2]*dw;
	        }
	        else
		        Q[cant_ptos-1] = Q[0];
	

	        VERTEX2D []pt = new VERTEX2D [4000];
	        int t = 0;
	        for(int i=0;i<cant_ptos-1;++i)
	        {
		        // 1er triangulo
		        pt[t].x = P[i].X;
		        pt[t].y = P[i].Y;
		        pt[t+1].x = Q[i].X;
		        pt[t+1].y = Q[i].Y;
		        pt[t+2].x = P[i+1].X;
		        pt[t+2].y = P[i+1].Y;


		        // segundo triangulo
		        pt[t+3].x = Q[i].X;
		        pt[t+3].y = Q[i].Y;
		        pt[t+4].x = P[i+1].X;
		        pt[t+4].y = P[i+1].Y;
		        pt[t+5].x = Q[i+1].X;
		        pt[t+5].y = Q[i+1].Y;

		        for(int j=0;j<6;++j)
		        {
			        pt[t].z = 0.5f;
			        pt[t].rhw = 1;
			        pt[t].color = color.ToArgb();
			        ++t;
		        }
	        }

	        Transform(pt,t);

	        // dibujo como lista de triangulos
            Device d3dDevice = GuiController.Instance.D3dDevice;
	        d3dDevice.VertexFormat = VertexFormats.Transformed | VertexFormats.Diffuse;
	        d3dDevice.DrawUserPrimitives(PrimitiveType.TriangleList,2*(cant_ptos-1),pt);

        }

        public void DrawSolidPoly(Vector2 []P,int cant_ptos,Color color,bool gradiente=true)
        {
	        // calculo el centro de gravedad
	        float xc = 0;
	        float yc = 0;
	        float ymin = 100000;
	        float ymax = -100000;

	        for(int i=0;i<cant_ptos-1;++i)
	        {
		        xc+=P[i].X;
		        yc+=P[i].Y;

		        if(P[i].Y>ymax)
			        ymax = P[i].Y;
		        if(P[i].Y<ymin)
			        ymin = P[i].Y;

	        }

	        xc/=(float )(cant_ptos-1);
	        yc/=(float )(cant_ptos-1);

	        float dy = Math.Max(1,ymax - ymin);

	        byte a =  color.A;
	        byte r =  color.R;
	        byte g =  color.G;
	        byte b =  color.B;

	        VERTEX2D []pt = new VERTEX2D [4000];
	        pt[0].x = xc;
	        pt[0].y = yc;
	        for(int i=0;i<cant_ptos;++i)
	        {
		        pt[i+1].x = P[i].X;
		        pt[i+1].y = P[i].Y;
	        }

	        for(int i=0;i<cant_ptos+1;++i)
	        {
		        pt[i].z = 0.5f;
		        pt[i].rhw = 1;
		        if(gradiente)
		        {
			        double k = 1 - (pt[i].y - ymin) / dy * 0.5;
                    pt[i].color = Color.FromArgb(a,(byte)Math.Min(255, r * k), (byte)Math.Min(255, g * k), (byte)Math.Min(255, r * b)).ToArgb();
		        }
		        else
			        pt[i].color = color.ToArgb();
	        }

	        Transform(pt,cant_ptos+1);

            Device d3dDevice = GuiController.Instance.D3dDevice;
            d3dDevice.VertexFormat = VertexFormats.Transformed | VertexFormats.Diffuse;
            d3dDevice.DrawUserPrimitives(PrimitiveType.TriangleFan, cant_ptos - 1, pt);

        }

        public void RoundRect(int x0,int y0,int x1,int y1,int r,int dw,Color color,bool solid=false)
        {
	        if(dw<1)
		        dw = 1;
	        Vector2  []pt = new Vector2[1000];

	        float da = M_PI/8.0f;
	        float alfa;
	
	        int t =0;
	        float x = x0;
	        float y = y0;
	        for(alfa =0;alfa<M_PI_2;alfa+=da)
	        {
                pt[t].X = x - r * (float)Math.Cos(alfa);
                pt[t].Y = y - r * (float)Math.Sin(alfa);
		        ++t;
	        }
	        pt[t].X = x;
	        pt[t].Y = y-r;
	        ++t;

	        x = x1;
	        y = y0;
	        for(alfa =M_PI_2;alfa<M_PI;alfa+=da)
	        {
                pt[t].X = x - r * (float)Math.Cos(alfa);
                pt[t].Y = y - r * (float)Math.Sin(alfa);
		        ++t;
	        }
	        pt[t].X = x+r;
	        pt[t].Y = y;
	        ++t;

	        x = x1;
	        y = y1;
	        for(alfa =0;alfa<M_PI_2;alfa+=da)
	        {
                pt[t].X = x + r * (float)Math.Cos(alfa);
                pt[t].Y = y + r * (float)Math.Sin(alfa);
                ++t;
	        }
	        pt[t].X = x;
	        pt[t].Y = y+r;
	        ++t;

	        x = x0;
	        y = y1;
	        for(alfa =M_PI_2;alfa<M_PI;alfa+=da)
	        {
                pt[t].X = x + r * (float)Math.Cos(alfa);
                pt[t].Y = y + r * (float)Math.Sin(alfa);
                ++t;
	        }
	        pt[t++] = pt[0];

	        if(solid)
		        DrawSolidPoly(pt,t,color,false);
	        else
		        DrawPoly(pt,t,dw,color);
        }


        public void DrawRect(int x0, int y0, int x1, int y1, int dw, Color color, bool solid = false)
        {
            if (dw < 1)
                dw = 1;
            Vector2[] pt = new Vector2[5];

            pt[0].X = x0;
            pt[0].Y = y0;
            pt[1].X = x1;
            pt[1].Y = y0;
            pt[2].X = x1;
            pt[2].Y = y1;
            pt[3].X = x0;
            pt[3].Y = y1;
            pt[4] = pt[0];

            if (solid)
                DrawSolidPoly(pt, 5, color, false);
            else
                DrawPoly(pt, 5, dw, color);
        }

        public void DrawDisc(Vector2 c, int r, Color color)
        {
            // demasiado peque�o el radio
            if(r<10)
                return;

            // quiero que cada linea como maximo tenga 3 pixeles
            float da = 3.0f / (float)r;
            int cant_ptos = (int)(2 * M_PI / da);

            VERTEX2D[] pt = new VERTEX2D[cant_ptos + 10];           // + 10 x las dudas
            
            int t = 0;              // Cantidad de vertices
            // el primer vertice es el centro del circulo
            pt[t].x = c.X;
            pt[t].y = c.Y;
            ++t;
            for (int i = 0; i < cant_ptos; ++i)
            {
                float an = (float)i / (float)cant_ptos * 2 * M_PI;
                pt[t].x = c.X + (float)Math.Cos(an) * r;
                pt[t].y = c.Y + (float)Math.Sin(an) * r;
                ++t;
            }
            pt[t++] = pt[1];      // Cierro el circulo

            for (int j = 0; j < t; ++j)
            {
                pt[j].z = 0.5f;
                pt[j].rhw = 1;
                pt[j].color = color.ToArgb();
            }

            Transform(pt, t);

            Device d3dDevice = GuiController.Instance.D3dDevice;
            d3dDevice.VertexFormat = VertexFormats.Transformed | VertexFormats.Diffuse;
            d3dDevice.DrawUserPrimitives(PrimitiveType.TriangleFan, t - 2, pt);

        }

        public void DrawCircle(Vector2 c, int r,int esp, Color color)
        {
            // demasiado peque�o el radio
            if (r - esp <10)
                return;

            // quiero que cada linea como maximo tenga 5 pixeles
            float da = 5.0f / (float)r;
            int cant_ptos = (int)(2 * M_PI / da);

            VERTEX2D[] pt = new VERTEX2D[2*cant_ptos + 10];           // + 10 x las dudas

            int t = 0;              // Cantidad de vertices

            
            for (int i = 0; i < cant_ptos; ++i)
            {
                float an = (float)i / (float)cant_ptos * 2 * M_PI;
                   
                // alterno los radios interior y exterior entre los pares e impares

                pt[t].x = c.X + (float)Math.Cos(an) * r;
                pt[t].y = c.Y + (float)Math.Sin(an) * r;
                ++t;

                pt[t].x = c.X + (float)Math.Cos(an) * (r - esp);
                pt[t].y = c.Y + (float)Math.Sin(an) * (r - esp);
                ++t;

            }

            pt[t++] = pt[0];      // Cierro el circulo
            pt[t++] = pt[1];      // Cierro el circulo

            for (int j = 0; j < t; ++j)
            {
                pt[j].z = 0.5f;
                pt[j].rhw = 1;
                pt[j].color = color.ToArgb();
            }

            Transform(pt, t);

            Device d3dDevice = GuiController.Instance.D3dDevice;
            d3dDevice.VertexFormat = VertexFormats.Transformed | VertexFormats.Diffuse;
            d3dDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, t - 2, pt);

        }


        // Helper para cargar una textura 
        public static Texture cargar_textura(String filename,bool alpha_channel=false)
        {
            Texture textura = null;
            filename.TrimEnd();
		    // cargo la textura
            Device d3dDevice = GuiController.Instance.D3dDevice;
            String fname_aux = GuiController.Instance.ExamplesMediaDir + "gui\\" + filename;
            if(!File.Exists(fname_aux))
                // Pruebo con la carpeta de texturas
                fname_aux = GuiController.Instance.ExamplesMediaDir + "focus\\texturas\\" + filename;
            if (!File.Exists(fname_aux))
                // Pruebo con el nombre directo
                fname_aux = filename;

            if (!File.Exists(fname_aux))
                return null;            // File doesnt exist


            try
            {
                if (alpha_channel)
                {
                    textura = TextureLoader.FromFile(d3dDevice, fname_aux, -2, -2, 1, Usage.None,
                        Format.A8B8G8R8,Pool.Managed,Filter.None,Filter.None,0);
                    // Mask transparente
                    SetAlphaChannel(textura, 255, 0, 255);
                }
                else
                    textura = TextureLoader.FromFile(d3dDevice, fname_aux);
            }
            catch (System.Exception error)
            {
            }
            return textura;
        }



        public static int SetAlphaChannel(Texture g_pTexture, byte r0, byte g0, byte b0)
        {
            // Initialize the alpha channel
            // tengo que hacer el reemplazo en TODOS los mipmaps generados para esta textura
            int cant_mipmaps = g_pTexture.LevelCount;
            for (int i = 0; i < cant_mipmaps; ++i)
            {
                SurfaceDescription desc = g_pTexture.GetLevelDescription(i);
                int m_dwWidth = desc.Width;
                int m_dwHeight = desc.Height;
                Surface surf = g_pTexture.GetSurfaceLevel(i);
                int pitch;
                GraphicsStream gs = surf.LockRectangle(LockFlags.Discard, out pitch);
                int size = m_dwHeight * pitch;
                byte[] buffer = new byte[size];
                gs.Read(buffer, 0, size);
                surf.UnlockRectangle();

                for (int y = 0; y < m_dwHeight; y++)
                {
                    for (int x = 0; x < m_dwWidth; x++)
                    {
                        int dwOffset = y * pitch + x*4;
                        byte b = buffer[dwOffset];
                        byte g = buffer[dwOffset + 1];
                        byte r = buffer[dwOffset + 2];
                        byte a;
                        if (Math.Abs(b - b0) < 15 && Math.Abs(g - g0) < 15 && Math.Abs(r - r0) < 15)		// es el mask transparente
                            a = 0;
                        else
                            a = 255;
                        buffer[dwOffset + 3] = a;
                    }
                }

                gs = surf.LockRectangle(LockFlags.Discard);
                gs.Write(buffer, 0, size);
                surf.UnlockRectangle();
            }

            return 1;
        }

         Matrix rectToQuad(float X,float Y,float W,float H,
                            float x1, float y1,float x2, float y2,float x3, float y3,float x4, float y4)
        {
            float y21 = y2 - y1;
            float y32 = y4 - y2;
            float y43 = y3 - y4;
            float y14 = y1 - y3;
            float y31 = y4 - y1;
            float y42 = y3 - y2;

            float a = -H*(x2*x4*y14 + x2*x3*y31 - x1*x3*y32 + x1*x4*y42);
            float b = W*(x2*x4*y14 + x4*x3*y21 + x1*x3*y32 + x1*x2*y43);
            float c = H*X*(x2*x4*y14 + x2*x3*y31 - x1*x3*y32 + x1*x4*y42) - H*W*x1*(x3*y32 - x4*y42 + x2*y43) - W*Y*(x2*x4*y14 + x4*x3*y21 + x1*x3*y32 + x1*x2*y43);

            float d = H*(-x3*y21*y4 + x2*y1*y43 - x1*y2*y43 - x4*y1*y3 + x4*y2*y3);
            float e = W*(x3*y2*y31 - x4*y1*y42 - x2*y31*y3 + x1*y4*y42);
            float f = -(W*(x3*(Y*y2*y31 + H*y1*y32) - x4*(H + Y)*y1*y42 + H*x2*y1*y43 + x2*Y*(y1 - y4)*y3 + x1*Y*y4*(-y2 + y3)) - H*X*(x3*y21*y4 - x2*y1*y43 + x4*(y1 - y2)*y3 + x1*y2*(-y4 + y3)));

            float g = H*(x4*y21 - x3*y21 + (-x1 + x2)*y43);
            float h = W*(-x2*y31 + x3*y31 + (x1 - x4)*y42);
            float i = W*Y*(x2*y31 - x3*y31 - x1*y42 + x4*y42) + H*(X*(-(x4*y21) + x3*y21 + x1*y43 - x2*y43) + W*(-(x4*y2) + x3*y2 + x2*y4 - x3*y4 - x2*y3 + x4*y3));

            const double ep = 0.0001;

            if(Math.Abs(i) < ep)
            {
                i = (float)(ep* (i > 0 ? 1.0f : -1.0f));
            }

            Matrix transform = new Matrix();

             // X
            transform.M11 = a / i;
            transform.M21 = b / i;
            transform.M31 = 0;
            transform.M41 = c / i;
             // Y
            transform.M12 = d / i;
            transform.M22 = e / i;
            transform.M32 = 0;
            transform.M42 = f / i;
            
             // Z
            transform.M13 = 0;
            transform.M23 = 0;
            transform.M33 = 1;
            transform.M43 = 0;

             // W
            transform.M14 = g / i;
            transform.M24 = h / i;
            transform.M34 = 0;
            transform.M44 = 1.0f;

            return transform;
        }
    }
    
}