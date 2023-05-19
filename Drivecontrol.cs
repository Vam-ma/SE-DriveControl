        string ControllerTag = "[DCV2]";                    // set tag to main cockpit
        string LCDTAG = "[DCV2LCD]";                        // set tag to surface block name
        bool fixLCD = false;                                // set true if surface is written incorrectly
        int surfaceIndex = 0;                               // if controller has multiple surfaces, you can increase index to switch surface
        int UnitType = 1;                                   //0 = m/s, 1 = Km/h, 2 = Mp/h
        float SpeedLimit = 23;                              // speedlimit at m/s
        float MaxSteeringAngle = 30f;                       // Front suspension max steering angle 
        bool HideBackground = true;                         // Hides the background image

        Vehicle vehicle;
        public Program()
        {
            List<IMyShipController> controllers = new List<IMyShipController>();
            List<IMyMotorSuspension> suspensions = new List<IMyMotorSuspension>();
            List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            List<IMyTextSurface> surfaces = new List<IMyTextSurface>();
            GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers, x => x.CustomName.Contains(ControllerTag));
            GridTerminalSystem.GetBlocksOfType<IMyMotorSuspension>(suspensions);
            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
            GridTerminalSystem.GetBlocksOfType<IMyTextSurfaceProvider>(blocks, x => x.CustomName.Contains(LCDTAG));
            foreach(var block in blocks)
            {
                IMyTextSurface surface = ((IMyTextSurfaceProvider)block).GetSurface(surfaceIndex);
                surfaces.Add(surface);
            }
            vehicle = new Vehicle(controllers[0], suspensions, batteries, surfaces, SpeedLimit, MaxSteeringAngle);
            vehicle.fixLCD = fixLCD;
            vehicle.Units = UnitType;
            vehicle.HideBackground = HideBackground;
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            vehicle.Control();
        }
    }
    class Vehicle
    {
        public IMyShipController Controller { get; set; }
        public List<IMyMotorSuspension> Suspensions { get; set; }
        public List<IMyBatteryBlock> Batteries { get; set; }
        public List<IMyTextSurface> Surfaces { get; set; }
        public float MaxSteeringAngle { get; set; }
        public float MaxSpeed { get; set; }
        public bool fixLCD { get; set; }
        public int Units { get; set; }
        public bool HideBackground { get; set; }
        RadConverter converter = new RadConverter();
        public Vehicle(IMyShipController controller, List<IMyMotorSuspension> suspensions, List<IMyBatteryBlock> batteries, List<IMyTextSurface> surfaces, float maxSpeed, float maxSteeringAngle)
        {
            Suspensions = new List<IMyMotorSuspension>();
            Batteries = new List<IMyBatteryBlock>();
            Surfaces = new List<IMyTextSurface>();
            this.Controller = controller;
            this.Batteries = batteries;
            this.Surfaces = surfaces;

            this.MaxSpeed = maxSpeed;
            this.MaxSteeringAngle = maxSteeringAngle;

                // Setup surfaces
            foreach(var surface in Surfaces)
            {
                surface.ContentType = ContentType.SCRIPT;
                surface.Script = "";
                surface.ScriptBackgroundColor = Color.Black;
            }
                // Left & Right Flipped?
            Vector3 front = Controller.CenterOfMass + Controller.WorldMatrix.Forward * 50;
            Vector3 back = Controller.CenterOfMass + Controller.WorldMatrix.Backward * 50;
            Vector3 left = Controller.CenterOfMass + Controller.WorldMatrix.Left * 30;
            Vector3 right = Controller.CenterOfMass + Controller.WorldMatrix.Right * 30;

            Vector3 frontleft = front + (Vector3.Distance(front,left)/2 * Vector3.Normalize(front - left));
            Vector3 frontright = front + (Vector3.Distance(front, right) / 2 * Vector3.Normalize(front - right));
            Vector3 backleft = back + (Vector3.Distance(back, left) / 2 * Vector3.Normalize(back - left));
            Vector3 backright = back + (Vector3.Distance(back, right) / 2 * Vector3.Normalize(back - right));

                // Add Front Wheels
            IMyMotorSuspension F = suspensions[0];
            foreach (var sus in suspensions) if (Vector3.Distance(sus.GetPosition(), frontleft) < Vector3.Distance(F.GetPosition(), frontleft)) F = sus;
            Suspensions.Add(F);
            foreach (var sus in suspensions) if (Vector3.Distance(sus.GetPosition(), frontright) < Vector3.Distance(F.GetPosition(), frontright)) F = sus;
            Suspensions.Add(F);
                // Calculate Rear Wheels
            List<IMyMotorSuspension> Rear = new List<IMyMotorSuspension>();
            foreach (var sus in suspensions) if (Vector3.Distance(sus.GetPosition(), backleft) < Vector3.Distance(F.GetPosition(), backleft)) F = sus;
            Rear.Add(F);
            foreach (var sus in suspensions) if (Vector3.Distance(sus.GetPosition(), backright) < Vector3.Distance(F.GetPosition(), backright)) F = sus;
            Rear.Add(F);
                // Calculate & add center Wheels
            List<IMyMotorSuspension> temp = new List<IMyMotorSuspension>();
            foreach (var sus in suspensions) if (!Suspensions.Contains(sus) && !Rear.Contains(sus)) temp.Add(sus);
            for (int i = 0; i < (suspensions.Count / 2 - 2); i++)
            {
                IMyMotorSuspension newsus = temp[0];
                foreach (var sus in temp) if (Vector3.Distance(sus.GetPosition(), frontleft) < Vector3.Distance(newsus.GetPosition(), frontleft)) newsus = sus;
                Suspensions.Add(newsus);
                newsus.Steering = false;
                foreach (var sus in temp) if (Vector3.Distance(sus.GetPosition(), frontright) < Vector3.Distance(newsus.GetPosition(), frontright)) newsus = sus;
                Suspensions.Add(newsus);
                newsus.Steering = false;
                List<IMyMotorSuspension> temp2 = new List<IMyMotorSuspension>();
                foreach(var sus in temp) if (!Suspensions.Contains(sus)) temp2.Add(sus);
                temp = temp2;
            }
                // Add Rear Wheels
            Suspensions.Add(Rear[0]);
            Suspensions.Add(Rear[1]);
            for (int i = 0; i < Suspensions.Count; i++)
            {
                Suspensions[i].CustomData = $"{i}";
                Suspensions[i].AirShockEnabled = false;
                Suspensions[i].IsParkingEnabled = true;
            }
        }
        public Vector2 GetOrientationPercentage()
        {
            Vector3 Center = Controller.CenterOfMass;
            MatrixD matrix = Controller.WorldMatrix;

            Vector3D planetD = new Vector3D();
            Controller.TryGetPlanetPosition(out planetD);

            Vector3 planet = new Vector3() { X = (float)planetD.X, Y = (float)planetD.Y, Z = (float)planetD.Z };

            Vector2 V = new Vector2()
            {
                X = 100 / 90 * Vector3.Distance(Center + matrix.Left * 45, planet) - Vector3.Distance(Center + matrix.Right * 45, planet),
                Y = 100 / 90 * Vector3.Distance(Center + matrix.Forward * 45, planet) - Vector3.Distance(Center + matrix.Backward * 45, planet)
            };
            return V;
        }
        int Refresh;
        public void Control()
        {
            if (Controller.IsUnderControl)
            {
                Vector3D velocityDir = Vector3D.Rotate(Controller.GetShipVelocities().LinearVelocity, MatrixD.Transpose(Controller.WorldMatrix));
                Vector3D rotationDir = Vector3D.Rotate(Controller.GetShipVelocities().AngularVelocity, MatrixD.Transpose(Controller.WorldMatrix));
                Vector3 controlDir = Controller.MoveIndicator;
                Vector2 orientation = GetOrientationPercentage();
                float velocity = (float)Controller.GetShipSpeed();
                float mass = Controller.CalculateShipMass().TotalMass;
                float massMultiplier = 1-(1 / 80000 * mass);
                float currentPower = 0;
                foreach(var battery in Batteries)
                {
                    currentPower += battery.CurrentStoredPower;
                }
                currentPower /= Batteries.Count;
                for (int i = 0; i < Suspensions.Count; i++)
                {
                    SetFriction(i, velocityDir, rotationDir, controlDir, velocity);
                    SetPower(i, velocity, rotationDir);
                    SetStrength(i, velocityDir, orientation, mass, massMultiplier);
                }
                SetSteering();
                if (Refresh == 0)
                {
                    WriteLCD(velocityDir, rotationDir, velocity, currentPower);
                }
                Refresh++;
                if (Refresh >= 10) Refresh = 0;
            }
            else
            {
                Controller.HandBrake = true;
            }
        }
        public void SetStrength(int i,Vector3 velocityDirection, Vector2 orientation, float mass, float massMultiplier)
        {
            // x- movement left, x+ movement right
            // y+ movement up, y- movement down
            // z- forward, z+ backward
            float baseStrenght = 250f;
            if (Suspensions[i].CubeGrid.GridSizeEnum == MyCubeSize.Large) baseStrenght = 7500f;
            float velocityDown = velocityDirection.Y * -1;
            if (velocityDown < 1) velocityDown = 1;
            if (i % 2 == 0 || i == 0)            // wheels right
            {
                if (i <= 1)                             // front right
                {
                    if (Suspensions[i].Strength < ((mass * velocityDown) / (baseStrenght * Suspensions.Count) * GetRatio(orientation.X) * GetRatio(-orientation.Y)) * massMultiplier)
                    {
                        Suspensions[i].Strength += 0.1f;
                    }
                    else
                    {
                        Suspensions[i].Strength = ((mass * velocityDown) / (baseStrenght * Suspensions.Count) * GetRatio(orientation.X) * GetRatio(-orientation.Y)) * massMultiplier;
                    }
                }
                else if (i >= Suspensions.Count - 2)     // Rear right
                {
                    if (Suspensions[i].Strength < ((mass * velocityDown) / (baseStrenght * Suspensions.Count) * GetRatio(orientation.X) * GetRatio(orientation.Y)) * massMultiplier)
                    {
                        Suspensions[i].Strength += 0.1f;
                    }
                    else
                    {
                        Suspensions[i].Strength = ((mass * velocityDown) / (baseStrenght * Suspensions.Count) * GetRatio(orientation.X) * GetRatio(orientation.Y)) * massMultiplier;
                    }
                }
                else                                    // Center right
                {
                    if (Suspensions[i].Strength < ((mass * velocityDown) / (baseStrenght * Suspensions.Count) * GetRatio(orientation.X)) * massMultiplier * 0.5f) 
                    {
                        Suspensions[i].Strength += 0.1f;
                    }
                    else
                    {
                        Suspensions[i].Strength = ((mass * velocityDown) / (baseStrenght * Suspensions.Count) * GetRatio(orientation.X)) * massMultiplier * 0.5f;
                    }
                }
            }
            else                                // Wheels Left
            {
                if (i <= 1)                             // front Left
                {
                    if (Suspensions[i].Strength < ((mass * velocityDown) / (baseStrenght * Suspensions.Count) * GetRatio(-orientation.X) * GetRatio(-orientation.Y)) * massMultiplier)
                    {
                        Suspensions[i].Strength += 0.1f;
                    }
                    else
                    {
                        Suspensions[i].Strength = ((mass * velocityDown) / (baseStrenght * Suspensions.Count) * GetRatio(-orientation.X) * GetRatio(-orientation.Y)) * massMultiplier;
                    }
                }
                else if (i >= Suspensions.Count - 2)     // Rear Left
                {
                    if (Suspensions[i].Strength < ((mass * velocityDown) / (baseStrenght * Suspensions.Count) * GetRatio(-orientation.X) * GetRatio(orientation.Y)) * massMultiplier)
                    {
                        Suspensions[i].Strength += 0.1f;
                    }
                    else
                    {
                        Suspensions[i].Strength = ((mass * velocityDown) / (baseStrenght * Suspensions.Count) * GetRatio(-orientation.X) * GetRatio(orientation.Y)) * massMultiplier;
                    }
                }
                else                                    // Center Left
                {
                    if (Suspensions[i].Strength < ((mass * velocityDown) / (baseStrenght * Suspensions.Count) * GetRatio(-orientation.X)) * massMultiplier * 0.5f)
                    {
                        Suspensions[i].Strength += 0.1f;
                    }
                    else
                    {
                        Suspensions[i].Strength = ((mass * velocityDown) / (baseStrenght * Suspensions.Count) * GetRatio(-orientation.X)) * massMultiplier * 0.5f;
                    }
                }
            }
            if (Suspensions[i].Strength > 80) Suspensions[i].Strength = 80;
        }
        public void SetFriction(int i, Vector3 velocityDirection, Vector3 rotationDirection, Vector3 controls, float velocity)
        {
            // x- movement left, x+ movement right
            // y+ movement up, y- movement down
            // z- forward, z+ backward

            // rotation direction
            // y- left, y+ right

            float SteeringMultRight = controls.X;
            float SteeringMultLeft = controls.X * -1;
            if (SteeringMultRight >= 0) SteeringMultRight = 1;
            if (SteeringMultLeft >= 0) SteeringMultLeft = 1;



            float movementX = velocityDirection.X;
            if (movementX < 0) movementX *= -1;

            if (i % 2 == 0 || i == 0)            // wheels right
            {
                if (Suspensions[i].Friction < (100 - velocity) * (1 - rotationDirection.Y) * (1 - (1 / velocity * velocityDirection.X)))
                {
                    Suspensions[i].Friction++;
                }
                else Suspensions[i].Friction = (100 - velocity) * (1 - rotationDirection.Y) * (1 - (1 / velocity * velocityDirection.X));
            }
            else                                // Wheels Left
            {
                if (Suspensions[i].Friction < (100 - velocity) * (1 - (rotationDirection.Y * -1)) * (1 - (1 / velocity * -velocityDirection.X)))
                {
                    Suspensions[i].Friction++;
                }
                else Suspensions[i].Friction = (100 - velocity) * (1 - (rotationDirection.Y * -1)) * (1 - (1 / velocity * -velocityDirection.X));
            }
            if (Suspensions[i].Friction < 20) Suspensions[i].Friction = 20;
            if (i % 2 == 0 || i == 0)
            {
                if(velocityDirection.X > 5) Suspensions[i].Friction = 0;
            }
            else
            {
                if(velocityDirection.X < -5) Suspensions[i].Friction = 0;
            }
        }
        public void SetPower(int i, float velocity, Vector3 rotationDirection)
        {
            if (i <= 1) Suspensions[i].Power = (100 - velocity) * 0.7f;
            else if (i <= Suspensions.Count - 3) Suspensions[i].Power = ((100 - velocity) * 1.0f) * (1 - rotationDirection.Z * 2);
            else Suspensions[i].Power = ((100 - velocity) * 0.9f) * (1 - rotationDirection.Z * 2);
            if (velocity > MaxSpeed) Suspensions[i].Power = 0;
        }
        public void SetSteering()
        {
            if (Controller.MoveIndicator.X < 0)
            {
                Suspensions[1].MaxSteerAngle = converter.DegToRad(MaxSteeringAngle);
                Suspensions[0].MaxSteerAngle = converter.DegToRad(MaxSteeringAngle / 2);
                Suspensions[Suspensions.Count - 1].MaxSteerAngle = converter.DegToRad(100 / MaxSteeringAngle * 5);
                Suspensions[Suspensions.Count - 2].MaxSteerAngle = converter.DegToRad(100 / MaxSteeringAngle * 3);
            }
            else if (Controller.MoveIndicator.X > 0)
            {
                Suspensions[1].MaxSteerAngle = converter.DegToRad(MaxSteeringAngle / 2);
                Suspensions[0].MaxSteerAngle = converter.DegToRad(MaxSteeringAngle);
                Suspensions[Suspensions.Count - 1].MaxSteerAngle = converter.DegToRad(100 / MaxSteeringAngle * 3);
                Suspensions[Suspensions.Count - 2].MaxSteerAngle = converter.DegToRad(100 / MaxSteeringAngle * 5);
            }
            else
            {
                Suspensions[1].MaxSteerAngle = converter.DegToRad(MaxSteeringAngle / 2);
                Suspensions[0].MaxSteerAngle = converter.DegToRad(MaxSteeringAngle / 2);
                Suspensions[Suspensions.Count - 1].MaxSteerAngle = converter.DegToRad(100 / MaxSteeringAngle * 3);
                Suspensions[Suspensions.Count - 2].MaxSteerAngle = converter.DegToRad(100 / MaxSteeringAngle * 3);
            }
        }
        public float GetRatio(float percentage)
        {
            return 1 + (percentage / 100);
        }
        int colorRefresh;
        public void WriteLCD(Vector3 velocityDirection, Vector3 rotationDirection, float velocity, float power)
        {
            int centerwheelcount = Suspensions.Count - 4;
            foreach(var surface in Surfaces)
            {
                var frame = surface.DrawFrame();

                Color frameColors = Color.MidnightBlue;
                colorRefresh++;
                if (colorRefresh >= 100)
                {
                    frameColors += new Color(0, 0, 1);
                    colorRefresh = 0;
                }
                Vector2 pos = new Vector2(surface.SurfaceSize.X / 2, surface.SurfaceSize.Y / 2 - 30);
                if (fixLCD) pos.Y += 62.5f;

                float showVelocity = velocity;
                string Unit = "m/s";
                switch (Units)
                {
                    case 1:
                        showVelocity = converter.MsToKmh(velocity);
                        Unit = "Km/h";
                        break;
                    case 2:
                        showVelocity = converter.MsToMph(velocity);
                        Unit = "Mp/h";
                        break;
                }
                int powerIndex = 1;
                if (power >= 0.8f) powerIndex = 4;
                else if (power >= 0.6f) powerIndex = 3;
                else if (power >= 0.4f) powerIndex = 2;
                Vector2 size = new Vector2(50, 50);
                if (surface.SurfaceSize.X > 256) size = new Vector2(100, 100);
                if(!HideBackground) DrawFrame(ref frame, surface, false, TextAlignment.CENTER, "LCD_Economy_Clear", pos + new Vector2(0, 30), Color.White, new Vector2(surface.SurfaceSize.X, surface.SurfaceSize.Y));
                DrawFrame(ref frame, surface, false, TextAlignment.CENTER, "CircleHollow", pos, frameColors, size);
                DrawFrame(ref frame, surface, true, TextAlignment.CENTER, $"{Math.Floor(showVelocity)}{Unit}", pos - new Vector2(0,5), Color.DeepSkyBlue);

                pos = new Vector2(surface.SurfaceSize.X / 2, surface.SurfaceSize.Y / 2 + 30);
                if (fixLCD) pos.Y += 2.5f;
                if (surface.SurfaceSize.X > 256)
                {
                    DrawFrame(ref frame, surface, false, TextAlignment.CENTER, "IconEnergy", pos + new Vector2(-70, 200), frameColors, new Vector2(40, 40));
                    DrawFrame(ref frame, surface, false, TextAlignment.CENTER, "AH_TextBox", pos + new Vector2(+30, 200), frameColors, new Vector2(100, 40));
                }
                else
                {
                    DrawFrame(ref frame, surface, false, TextAlignment.CENTER, "IconEnergy", pos + new Vector2(-30, 80), frameColors, new Vector2(20, 20));
                    DrawFrame(ref frame, surface, false, TextAlignment.CENTER, "AH_TextBox", pos + new Vector2(+10, 80), frameColors, new Vector2(50, 20));
                }
                Vector2 offset = new Vector2(-6, 80);
                size = new Vector2(9, 15);
                if (surface.SurfaceSize.X > 256)
                {
                    offset = new Vector2(-3, 200);
                    size = new Vector2(18, 30);
                }
                for (int i = 0; i < powerIndex; i++)
                {
                    if (powerIndex == 1 && power < 0.2f)
                    {
                        if(colorRefresh==0) DrawFrame(ref frame, surface, false, TextAlignment.CENTER, "SquareTapered", pos + offset, Color.Red + new Color(0,0,1), size);
                        else DrawFrame(ref frame, surface, false, TextAlignment.CENTER, "SquareTapered", pos + offset, Color.Red, size);
                    }
                    else if (powerIndex < 2)
                    {
                        if (colorRefresh == 0) DrawFrame(ref frame, surface, false, TextAlignment.CENTER, "SquareTapered", pos + offset, Color.Yellow + new Color(0, 0, 1), size);
                        else DrawFrame(ref frame, surface, false, TextAlignment.CENTER, "SquareTapered", pos + offset, Color.Yellow, size);
                    }
                    else
                    {
                        if (colorRefresh == 0) DrawFrame(ref frame, surface, false, TextAlignment.CENTER, "SquareTapered", pos + offset, Color.Green + new Color(0, 0, 1), size);
                        else DrawFrame(ref frame, surface, false, TextAlignment.CENTER, "SquareTapered", pos + offset, Color.Green, size);
                    }
                    if (surface.SurfaceSize.X > 256) offset.X += 22;
                    else offset.X += 11;

                }

                
                Vector2 position = new Vector2() { X = 20, Y = 20 };
                for (int i = 0; i< Suspensions.Count;i++)
                {
                    Color TextColor = Color.LightBlue;
                    string data = 
                        $"Friction : {Math.Floor(Suspensions[i].Friction)}\n\r" +
                        $"Strenght : {Math.Floor(Suspensions[i].Strength)}\n\r" +
                        $"Power    : {Math.Floor(Suspensions[i].Power)}";

                    if (!Suspensions[i].IsFunctional) TextColor = Color.Red;

                    if (i % 2 == 0 || i == 0)
                    {
                        if (i <= 1 )    // Front wheels
                        {
                            pos = new Vector2() { X = surface.SurfaceSize.X - position.X * 3.8f, Y = position.Y };
                            if (surface.SurfaceSize.X > 256)
                            {
                                pos.X = surface.SurfaceSize.X - position.X * 7.6f;
                            }
                            if (fixLCD) pos.Y += 40;
                            DrawFrame(
                                ref frame, 
                                surface, 
                                true, 
                                TextAlignment.LEFT, 
                                data, 
                                pos,
                                TextColor);
                        }
                        else if (i >= Suspensions.Count - 2) // Rear wheels
                        {
                            pos = new Vector2() { X = surface.SurfaceSize.X - position.X * 3.8f, Y = surface.SurfaceSize.Y - position.Y * 2 };
                            if (surface.SurfaceSize.X > 256)
                            {
                                pos.Y = surface.SurfaceSize.Y - position.Y * 4;
                                pos.X = surface.SurfaceSize.X - position.X * 7.6f;
                            }
                            if (fixLCD) pos.Y += 45;
                            
                            DrawFrame(
                                ref frame, 
                                surface, 
                                true, 
                                TextAlignment.LEFT, 
                                data, 
                                pos,
                                TextColor);
                        }
                        else // Center wheels
                        {
                            pos = new Vector2() { X = surface.SurfaceSize.X - position.X * 3.8f, Y = surface.SurfaceSize.Y / 2 };
                            if (surface.SurfaceSize.X > 256)
                            {
                                pos.Y = surface.SurfaceSize.Y / 2.5f;
                                pos.X = surface.SurfaceSize.X - position.X * 7.6f;
                            }
                            if (fixLCD) pos.Y += 32.5f;
                            DrawFrame(
                                ref frame, 
                                surface,
                                true,
                                TextAlignment.LEFT,
                                data,
                                pos,
                                TextColor);
                        }
                    }
                    else
                    {
                        if (i <= 1)    // Front wheels
                        {
                            pos = new Vector2() { X = position.X, Y = position.Y };
                            if (fixLCD) pos.Y += 40;
                            DrawFrame(
                                ref frame, 
                                surface,
                                true,
                                TextAlignment.LEFT,
                                data,
                                pos,
                                TextColor);
                        }
                        else if (i >= Suspensions.Count - 2) // Rear wheels
                        {
                            pos = new Vector2() { X = position.X, Y = surface.SurfaceSize.Y - position.Y * 2 };
                            if (surface.SurfaceSize.X > 256) pos.Y = surface.SurfaceSize.Y - position.Y * 4;
                            if (fixLCD) pos.Y += 45;
                            DrawFrame(
                                ref frame, 
                                surface,
                                true,
                                TextAlignment.LEFT,
                                data,
                                pos,
                                TextColor);
                        }
                        else // Center wheels
                        {
                            pos = new Vector2() { X = position.X, Y = surface.SurfaceSize.Y / 2 };
                            if (surface.SurfaceSize.X > 256) pos.Y = surface.SurfaceSize.Y / 2.5f;
                            if (fixLCD) pos.Y +=32.5f;
                            DrawFrame(
                                ref frame, 
                                surface,
                                true,
                                TextAlignment.LEFT,
                                data,
                                pos,
                                TextColor);
                        }
                    }
                }
                frame.Dispose();
            }
        }
        public void DrawFrame(ref MySpriteDrawFrame frame, IMyTextSurface surface, bool text, TextAlignment alignment, string data, Vector2 position, Color color, Vector2 size = new Vector2())
        {
            float fontSize = 0.4f;
            if (surface.SurfaceSize.X > 256) fontSize = 0.8f;
            if (text)
            {
                var sprite = new MySprite()
                {
                    Alignment = alignment,
                    Type = SpriteType.TEXT,
                    RotationOrScale = fontSize,
                    Position = position,
                    Data = data,
                    Color = color
                };
                frame.Add(sprite);
            }
            else
            {
                var sprite = new MySprite()
                {
                    Alignment = alignment,
                    Type = SpriteType.TEXTURE,
                    Position = position,
                    Size = size,
                    Data = data,
                    Color = color
                };
                frame.Add(sprite);
            }
        }
    }
    class RadConverter
    {
        public float DegToRad(float value)
        {
            return value * ((float)Math.PI) / 180f;
        }
        public float RadToDeg(float value)
        {
            return value * 180f / ((float)Math.PI);
        }
        public float MsToKmh(float value)
        {
            return value * 3.6f;
        }
        public float MsToMph(float value)
        {
            return value * 2.23694f;
        }
