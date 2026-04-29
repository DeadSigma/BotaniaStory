using ProtoBuf;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace BotaniaStory
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TiaraStatePacket
    {
        public bool IsFlying;
        public bool ForceStop;
        public bool IsDashing; 
    }

    public class TiaraFlightSystem : ModSystem
    {
        private const int ManaCostPerTick = 50;
        private const int DashManaCost = 5000;
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;

        private TiaraParticleRenderer particleRenderer;
        private bool isGliding = false;
        private bool clientTiaraFlying = false;
        private bool wasJumpPressed = false;
        private long lastJumpTime = 0;

        //         НАСТРОЙКИ РЫВКА И ПОЛЕТА
        private const float DashForce = 5.0f;       // СИЛА РЫВКА
        private const float DashCost = 10.0f;       // Цена рывка
        private float dashCooldown = 0f;
        private bool wasSprintPressed = false;
        private float dashActiveTimer = 0f;

        // --- УСТАЛОСТЬ ---
        public const float MaxFlightTime = 60f;
        public float currentFlightTime = 60f;
        private bool isExhausted = false;

        private TiaraHud tiaraHud;

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel("tiaranetwork").RegisterMessageType<TiaraStatePacket>();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            sapi.Network.GetChannel("tiaranetwork").SetMessageHandler<TiaraStatePacket>((p, pkt) => {
                p.Entity.Attributes.SetBool("tiaraIsFlying", pkt.IsFlying);

                if (pkt.IsDashing)
                {
                    // Воспроизводим звук
                    var channel = sapi.Network.GetChannel("botanianetwork");
                    if (channel != null)
                    {
                        channel.BroadcastPacket(new PlayManaSoundPacket()
                        {
                            Position = new Vintagestory.API.MathTools.Vec3d(p.Entity.Pos.X, p.Entity.Pos.Y, p.Entity.Pos.Z),
                            SoundName = "whish"
                        });
                    }

                    // Вычитаем ману за рывок
                    ItemSlot tabletSlot = FindManaTablet(p);
                    ItemManaTablet tablet = tabletSlot?.Itemstack?.Item as ItemManaTablet;
                    if (tablet != null)
                    {
                        int currentMana = tablet.GetMana(tabletSlot.Itemstack);
                        if (currentMana >= DashManaCost)
                        {
                            tablet.SetMana(tabletSlot.Itemstack, currentMana - DashManaCost);
                            tabletSlot.MarkDirty();
                        }
                        else
                        {
                            // Если игрок как-то обошел клиентскую проверку, отключаем полет
                            tablet.SetMana(tabletSlot.Itemstack, 0);
                            tabletSlot.MarkDirty();
                            p.Entity.Attributes.SetBool("tiaraIsFlying", false);
                            sapi.Network.GetChannel("tiaranetwork").SendPacket(new TiaraStatePacket { ForceStop = true }, p);
                        }
                    }
                }
            });
            sapi.Event.RegisterGameTickListener(ServerManaTick, 100);
        }

        private void ServerManaTick(float dt)
        {
            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.Attributes.GetBool("tiaraIsFlying", false) == true)
                {
                    ItemSlot tabletSlot = FindManaTablet(player);
                    ItemManaTablet tablet = tabletSlot?.Itemstack?.Item as ItemManaTablet;
                    if (tablet != null && tablet.GetMana(tabletSlot.Itemstack) >= ManaCostPerTick)
                    {
                        tablet.SetMana(tabletSlot.Itemstack, tablet.GetMana(tabletSlot.Itemstack) - ManaCostPerTick);
                        tabletSlot.MarkDirty();
                    }
                    else
                    {
                        player.Entity.Attributes.SetBool("tiaraIsFlying", false);
                        sapi.Network.GetChannel("tiaranetwork").SendPacket(new TiaraStatePacket { ForceStop = true }, player);
                    }
                }
            }
        }


        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            capi.Network.GetChannel("tiaranetwork").SetMessageHandler<TiaraStatePacket>(pkt => {
                if (pkt.ForceStop) { clientTiaraFlying = false; isExhausted = true; }
            });
            particleRenderer = new TiaraParticleRenderer(api);
            tiaraHud = new TiaraHud(api);
            api.Gui.RegisterDialog(tiaraHud);
            capi.Event.RegisterGameTickListener(ClientInputTick, 20);
        }

        private void ClientInputTick(float dt)
        {
            var player = capi.World?.Player;
            if (player?.Entity == null) return;

            bool isGrounded = player.Entity.OnGround || player.Entity.CollidedVertically || player.Entity.Swimming;

            if (isGrounded)
            {
                isExhausted = false; // Усталость сбрасывается только при касании земли
                if (clientTiaraFlying) { clientTiaraFlying = false; SendStateToServer(false); }
                isGliding = false;
                player.Entity.AnimManager.StopAnimation("fly");
                player.Entity.AnimManager.StopAnimation("Creativefly");
            }

            if (!clientTiaraFlying && !isGliding)
            {
                if (currentFlightTime < MaxFlightTime)
                {
                    currentFlightTime += dt * 0.25f;
                }
            }

            if (dashCooldown > 0) dashCooldown -= dt;
            if (dashActiveTimer > 0) dashActiveTimer -= dt;

            tiaraHud.StaminaValue = currentFlightTime / MaxFlightTime;
            tiaraHud.IsTiaraEquipped = HasTiara(player);

            bool isJump = player.Entity.Controls.Jump;
            if (isJump && !wasJumpPressed)
            {
                if (capi.World.ElapsedMilliseconds - lastJumpTime < 300 && !isExhausted && HasTiara(player))
                {
                    if (clientTiaraFlying)
                    {
                        // Отключаем полет, если он уже активен (для этого мана не нужна)
                        clientTiaraFlying = false;
                        SendStateToServer(false);
                    }
                    else
                    {
                        // Пытаемся включить полет. Проверяем планшет и ману прямо на клиенте!
                        ItemSlot tabletSlot = FindManaTablet(player);
                        ItemManaTablet tablet = tabletSlot?.Itemstack?.Item as ItemManaTablet;

                        if (tablet != null && tablet.GetMana(tabletSlot.Itemstack) >= ManaCostPerTick)
                        {
                            clientTiaraFlying = true;
                            SendStateToServer(true);
                        }
                    }
                }
                lastJumpTime = capi.World.ElapsedMilliseconds; 
            }
            wasJumpPressed = isJump;

            if (clientTiaraFlying)
            {
                currentFlightTime -= dt;
                if (currentFlightTime <= 0 || !HasTiara(player)) { isExhausted = true; clientTiaraFlying = false; SendStateToServer(false); return; }

                particleRenderer.SpawnFlightParticles(player, dashActiveTimer > 0);

                player.Entity.AnimManager.StopAnimation("idle");
                player.Entity.AnimManager.StopAnimation("walk");
                player.Entity.AnimManager.StopAnimation("run");

                // Проверяем, нажимает ли игрок кнопки движения (включая движение назад для консистентности)
                bool isMovingHorizontally = player.Entity.Controls.Forward ||
                                            player.Entity.Controls.Left ||
                                            player.Entity.Controls.Right ||
                                            player.Entity.Controls.Backward;

                if (isMovingHorizontally)
                {
                    // Если игрок двигается по горизонтали — включаем анимацию планирования
                    player.Entity.AnimManager.StopAnimation("fly");
                    player.Entity.AnimManager.StartAnimation("Creativefly");
                }
                else
                {
                    // Если игрок просто висит в воздухе — оставляем анимацию обычного полета
                    player.Entity.AnimManager.StopAnimation("Creativefly");
                    player.Entity.AnimManager.StartAnimation("fly");
                }

                if (isJump) player.Entity.Pos.Motion.Y = 0.08f;
                else if (player.Entity.Controls.Sneak) player.Entity.Pos.Motion.Y = -0.08f;
                else if (player.Entity.Pos.Motion.Y < 0) player.Entity.Pos.Motion.Y = 0;

                // --- РЫВОК ---
                bool isSprint = player.Entity.Controls.Sprint;

                ItemSlot dashTabletSlot = FindManaTablet(player);
                ItemManaTablet dashTablet = dashTabletSlot?.Itemstack?.Item as ItemManaTablet;
                bool hasEnoughDashMana = dashTablet != null && dashTablet.GetMana(dashTabletSlot.Itemstack) >= DashManaCost;

                if (isSprint && !wasSprintPressed && dashCooldown <= 0 && currentFlightTime >= DashCost && hasEnoughDashMana)
                {
                    float yaw = player.Entity.Pos.Yaw;
                    // Применяем импульс силы DashForce
                    player.Entity.Pos.Motion.X += (float)Math.Sin(yaw) * DashForce;
                    player.Entity.Pos.Motion.Z += (float)Math.Cos(yaw) * DashForce;

                    currentFlightTime -= DashCost;
                    dashCooldown = 2.0f;
                    dashActiveTimer = 0.3f; // Блокировка стандартного управления на 0.3 сек (свободный полет)

                    capi.Network.GetChannel("tiaranetwork").SendPacket(new TiaraStatePacket
                    {
                        IsFlying = clientTiaraFlying,
                        IsDashing = true // Отправляем сигнал о рывке на сервер
                    });
                }
                wasSprintPressed = isSprint;

                if (dashActiveTimer <= 0)
                {
                    ApplyHorizontalMotion(player, 0.15f);
                }
            }
            else if (!isGrounded && player.Entity.Controls.Sneak && HasTiara(player))
            {
                // Начинаем планировать только если уже ощутимо падаем вниз (прыжок с утеса)
                // Если планирование уже началось (isGliding == true), то продолжаем
                if (isGliding || player.Entity.Pos.Motion.Y < -0.15f)
                {
                    isGliding = true; // Фиксируем состояние
                    particleRenderer.SpawnFlightParticles(player, false);
                    player.Entity.AnimManager.StopAnimation("fly");
                    player.Entity.AnimManager.StartAnimation("Creativefly");

                    if (player.Entity.Pos.Motion.Y < -0.03f) player.Entity.Pos.Motion.Y = -0.03f;

                    float yaw = player.Entity.Pos.Yaw;
                    player.Entity.Pos.Motion.X = (float)Math.Sin(yaw) * 0.18f;
                    player.Entity.Pos.Motion.Z = (float)Math.Cos(yaw) * 0.18f;
                }
            }
            else
            {
                // Если игрок в воздухе, но отпустил Shift или снял тиару - сбрасываем планирование
                isGliding = false;
            }
        }

        private void SendStateToServer(bool isFlying) => capi.Network.GetChannel("tiaranetwork").SendPacket(new TiaraStatePacket { IsFlying = isFlying });

        private void ApplyHorizontalMotion(IClientPlayer player, float speed)
        {
            float yaw = player.CameraYaw;

            float moveX = 0, moveZ = 0;
            if (player.Entity.Controls.Forward) { moveX += (float)Math.Sin(yaw); moveZ += (float)Math.Cos(yaw); }
            if (player.Entity.Controls.Backward) { moveX -= (float)Math.Sin(yaw); moveZ -= (float)Math.Cos(yaw); }
            if (player.Entity.Controls.Left) { moveX += (float)Math.Cos(yaw); moveZ -= (float)Math.Sin(yaw); }
            if (player.Entity.Controls.Right) { moveX -= (float)Math.Cos(yaw); moveZ += (float)Math.Sin(yaw); }

            if (moveX != 0 || moveZ != 0)
            {
                float len = (float)Math.Sqrt(moveX * moveX + moveZ * moveZ);
                float targetX = (moveX / len) * speed;
                float targetZ = (moveZ / len) * speed;

                // Считаем текущий квадрат скорости
                double currentSqSpeed = player.Entity.Pos.Motion.X * player.Entity.Pos.Motion.X + player.Entity.Pos.Motion.Z * player.Entity.Pos.Motion.Z;

                // Если текущая скорость значительно ВЫШЕ базовой (мы в инерции после рывка)
                if (currentSqSpeed > speed * speed + 0.01)
                {
                    // Плавное затухание скорости (Lerp). Игрок плавно тормозит об "воздух", пока скорость не упадет до нормы
                    player.Entity.Pos.Motion.X += (targetX - (float)player.Entity.Pos.Motion.X) * 0.1f;
                    player.Entity.Pos.Motion.Z += (targetZ - (float)player.Entity.Pos.Motion.Z) * 0.1f;
                }
                else
                {
                    // Обычный полет (жесткий контроль, без скольжения)
                    player.Entity.Pos.Motion.X = targetX;
                    player.Entity.Pos.Motion.Z = targetZ;
                }
            }
            else
            {
                // Инерционное торможение при отпущенных клавишах WASD
                player.Entity.Pos.Motion.X *= 0.8f;
                player.Entity.Pos.Motion.Z *= 0.8f;
            }
        }

        private bool HasTiara(IPlayer player)
        {
            // Получаем инвентарь экипировки персонажа (броня, одежда, аксессуары)
            var characterInv = player.InventoryManager.GetOwnInventory("character");

            if (characterInv != null)
            {
                foreach (var slot in characterInv)
                {
                    // Проверяем, есть ли в слотах экипировки наша тиара
                    if (slot.Itemstack?.Item is ItemFlightTiara)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private ItemSlot FindManaTablet(IPlayer player)
        {
            foreach (var inv in player.InventoryManager.Inventories.Values)
            {
                if (inv == null || inv.ClassName == "creative") continue;
                foreach (var slot in inv) if (slot.Itemstack?.Item is ItemManaTablet) return slot;
            }
            return null;
        }
    }
}