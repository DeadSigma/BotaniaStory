using botaniastory;
using BotaniaStory.blocks;
using BotaniaStory.client.renderers;
using BotaniaStory.entities;
using BotaniaStory.items;
using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BotaniaStory.blockentity
{
    public interface IManaReceiver
    {
        bool IsFull();
        void ReceiveMana(int amount); 
        int GetAvailableSpace();      
    }

    public class BlockEntityManaSpreader : BlockEntity, IManaReceiver
    {
        // углы поворота
        public float Yaw = 0f;
        public float Pitch = 0f;
        public int CurrentMana = 0;
        public int MaxMana = 1000; // макс емкость

        // координаты цели
        public BlockPos TargetPos = null;

        private bool isDischarging = false; 
        private long lastFireMs = 0; 
        private int fireCooldownMs = 500; 
        private int burstManaAmount = 190;


        public BlockEntityAnimationUtil animUtil;

        private SpreaderCoreRenderer coreRenderer;

        private MeshData baseMesh;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerTick, 100);
            }

            // рендер онли клиент
            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                coreRenderer = new SpreaderCoreRenderer(capi, Pos, this);
                capi.Event.RegisterRenderer(coreRenderer, EnumRenderStage.Opaque, "botaniastory");
            }
        }

        // реализация IManaReceiver
        public bool IsFull()
        {
            return CurrentMana >= MaxMana;
        }

        public void ReceiveMana(int amount)
        {
            CurrentMana += amount;
            if (CurrentMana > MaxMana) CurrentMana = MaxMana;

            // false - мана не влияет на модель
            MarkDirty(false);
        }

        public int GetAvailableSpace()
        {
            return MaxMana - CurrentMana;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            DisposeRenderer();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            DisposeRenderer();
        }

        private void DisposeRenderer()
        {
            if (Api is ICoreClientAPI capi && coreRenderer != null)
            {
                capi.Event.UnregisterRenderer(coreRenderer, EnumRenderStage.Opaque);
                coreRenderer.Dispose();
                coreRenderer = null;
            }

            baseMesh = null;
        }

        private void OnServerTick(float dt)
        {
            if (CurrentMana < MaxMana)
            {
                // проверяем все 6 сторон
                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    BlockPos adjPos = Pos.AddCopy(facing);

                    if (TargetPos != null && adjPos.Equals(TargetPos)) continue;

                    BlockEntity adjBlockEntity = Api.World.BlockAccessor.GetBlockEntity(adjPos);

                    if (adjBlockEntity is BlockEntityManaPool adjacentPool)
                    {
                        if (adjacentPool.CurrentMana > 0)
                        {
                            int neededMana = MaxMana - CurrentMana;

                            int manaToTake = Math.Min(neededMana, adjacentPool.CurrentMana);

                            this.CurrentMana += manaToTake;
                            adjacentPool.CurrentMana -= manaToTake;

                            this.MarkDirty(true);
                            adjacentPool.MarkDirty(true);

                            if (this.CurrentMana >= MaxMana)
                            {
                                break;
                            }
                        }
                    }
                }
            }


            if (TargetPos != null)
            {
                BlockEntity targetBlock = Api.World.BlockAccessor.GetBlockEntity(TargetPos);

                if (!(targetBlock is IManaReceiver))
                {
                    TargetPos = null;
                    MarkDirty(true);
                }
            }

            if (TargetPos == null)
            {
                double dy = Math.Sin(Pitch);
                double distanceXZ = Math.Cos(Pitch);
                double dx = -Math.Sin(Yaw) * distanceXZ;
                double dz = -Math.Cos(Yaw) * distanceXZ;

                for (float i = 1f; i <= 12f; i += 0.5f)
                {
                    int cx = (int)Math.Floor(Pos.X + 0.5 + dx * i);
                    int cy = (int)Math.Floor(Pos.Y + 0.5 + dy * i);
                    int cz = (int)Math.Floor(Pos.Z + 0.5 + dz * i);
                    BlockPos checkPos = new BlockPos(cx, cy, cz);

                    Block hitBlock = Api.World.BlockAccessor.GetBlock(checkPos);

                    if (hitBlock is BlockManaPool || hitBlock is BlockRunicAltar || hitBlock is BlockTerrestrialPlate || hitBlock is ManaSpreader)
                    {
                        if (!checkPos.Equals(Pos))
                        {
                            TargetPos = checkPos.Copy();
                            MarkDirty(true);
                            break;
                        }
                    }
                    else if (hitBlock.Id != 0 && hitBlock.CollisionBoxes != null && hitBlock.CollisionBoxes.Length > 0)
                    {
                        // скип прозрачных блоков
                        if (EntityManaBurst.IsManaPermeable(hitBlock))
                        {
                            continue;
                        }
                        break; // уперлись в стену
                    }
                }
            }

            // выстрел
            // порог разрядки 20%
            int threshold = (int)(MaxMana * 0.20f);

            if (CurrentMana >= threshold)
            {
                isDischarging = true;
            }

            // выкл если не хватает на выстрел
            if (CurrentMana < burstManaAmount)
            {
                isDischarging = false;
            }

            if (!isDischarging || TargetPos == null) return;

            // проверка кд
            long currentMs = Api.World.ElapsedMilliseconds;
            if (currentMs - lastFireMs < fireCooldownMs) return;

            BlockEntity receiverBlock = Api.World.BlockAccessor.GetBlockEntity(TargetPos);

            if (receiverBlock is IManaReceiver receiver)
            {
                int availableSpace = receiver.GetAvailableSpace();

                // 0 если рецепта нет или басик фулл
                if (availableSpace <= 0)
                {
                    return; 
                }

                // излишки сгорают при попадании
            }
            else
            {
                // отмена если интерфейса нет
                return;
            }

            Vec3d startPos = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
            Vec3d targetCenter = new Vec3d(TargetPos.X + 0.5, TargetPos.Y + 0.5, TargetPos.Z + 0.5);

            double distance = startPos.DistanceTo(targetCenter);
            Vec3d direction = (targetCenter - startPos).Normalize();

            bool isBlocked = false;

            // рейкаст к цели
            for (float step = 0.5f; step < distance - 0.2f; step += 0.5f)
            {
                BlockPos checkPos = new BlockPos(
                    (int)Math.Floor(startPos.X + direction.X * step),
                    (int)Math.Floor(startPos.Y + direction.Y * step),
                    (int)Math.Floor(startPos.Z + direction.Z * step)
                );

                if (checkPos.Equals(Pos)) continue;

                Block hitBlock = Api.World.BlockAccessor.GetBlock(checkPos);

                // чек хитбоксов
                if (hitBlock.Id != 0 && hitBlock.CollisionBoxes != null && hitBlock.CollisionBoxes.Length > 0)
                {
                    if (checkPos.Equals(TargetPos) || hitBlock is BlockManaPool)
                    {
                        break;
                    }

                    if (EntityManaBurst.IsManaPermeable(hitBlock))
                    {
                        continue;
                    }

                    isBlocked = true;
                    break;
                }
            }

            if (isBlocked) return;

            // спавн сгустка
            // тип энтити
            EntityProperties type = Api.World.GetEntityType(new AssetLocation("botaniastory", "manaburst"));
            if (type == null) return;

            // инит сгустка
            EntityManaBurst burstEntity = (EntityManaBurst)Api.World.ClassRegistry.CreateEntity(type);
            burstEntity.ManaPayload = burstManaAmount;
            burstEntity.SourcePos = Pos.Copy();

            // дальность
            burstEntity.WatchedAttributes.SetDouble("maxDist", 8.0);
            // цвет
            burstEntity.WatchedAttributes.SetInt("burstColor", 0x5CC94A);

            // спавн в центре дула
            burstEntity.Pos.SetPos(startPos);
            burstEntity.Pos.SetFrom(burstEntity.Pos);

            // старт координаты для клиента
            burstEntity.WatchedAttributes.SetDouble("startX", startPos.X);
            burstEntity.WatchedAttributes.SetDouble("startY", startPos.Y);
            burstEntity.WatchedAttributes.SetDouble("startZ", startPos.Z);

            // скорость в сек для фикса рассинхрона
            const double burstSpeed = 4.5;

            double motionX = direction.X * burstSpeed;
            double motionY = direction.Y * burstSpeed;
            double motionZ = direction.Z * burstSpeed;

            // апдейт велосити
            burstEntity.Pos.Motion.Set(motionX, motionY, motionZ);
            burstEntity.Pos.Motion.Set(motionX, motionY, motionZ);

            // синк скорости на клиент
            burstEntity.WatchedAttributes.SetDouble("motionX", motionX);
            burstEntity.WatchedAttributes.SetDouble("motionY", motionY);
            burstEntity.WatchedAttributes.SetDouble("motionZ", motionZ);

            Api.World.SpawnEntity(burstEntity);

            // пакет звука
            ICoreServerAPI sapi = Api as ICoreServerAPI;
            IServerNetworkChannel channel = sapi.Network.GetChannel("botanianetwork");

            PlayManaSoundPacket soundMessage = new PlayManaSoundPacket()
            {
                Position = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5),
                SoundName = "manaspreaderfire"
            };

            channel.BroadcastPacket(soundMessage);

            // апдейт стейта
            lastFireMs = currentMs;
            this.CurrentMana -= burstManaAmount;
            this.MarkDirty(true);
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("yaw", Yaw);
            tree.SetFloat("pitch", Pitch);
            tree.SetInt("mana", CurrentMana);

            if (TargetPos != null)
            {
                tree.SetInt("tgtX", TargetPos.X);
                tree.SetInt("tgtY", TargetPos.Y);
                tree.SetInt("tgtZ", TargetPos.Z);
                tree.SetBool("hasTarget", true); 
            }
            else
            {
                tree.SetBool("hasTarget", false);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            float oldYaw = Yaw;
            float oldPitch = Pitch;

            Yaw = tree.GetFloat("yaw", 0f);
            Pitch = tree.GetFloat("pitch", 0f);
            CurrentMana = tree.GetInt("mana", 0);

            if (tree.GetBool("hasTarget"))
            {
                TargetPos = new BlockPos(tree.GetInt("tgtX"), tree.GetInt("tgtY"), tree.GetInt("tgtZ"));
            }
            else
            {
                TargetPos = null; 
            }

            if (Api?.Side == EnumAppSide.Client)
            {
                // апдейт модели только при повороте фикс моргания хитбокса
                if (Yaw != oldYaw || Pitch != oldPitch)
                {
                    MarkDirty(true);
                }
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            // локальная ссылка от null ref
            MeshData source = baseMesh;

            if (source == null)
            {
                AssetLocation shapeLoc = new AssetLocation("botaniastory", "shapes/block/manaspreader.json");
                Shape shape = Api.Assets.TryGet(shapeLoc)?.ToObject<Shape>();

                // фоллбек рендера
                if (shape == null) return false;

                tesselator.TesselateShape(Block, shape, out source);
                if (source == null) return false;

                baseMesh = source;
            }

            MeshData mesh = source.Clone();

            Matrixf matrix = new Matrixf();
            matrix.Translate(0.5f, 0.5f, 0.5f)
                  .RotateY(Yaw)
                  .RotateX(Pitch)
                  .Translate(-0.5f, -0.5f, -0.5f);

            mesh.MatrixTransform(matrix.Values);
            mesher.AddMeshData(mesh);

            return true;
        }

        // hud инфо
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            // активный айтем
            Item activeItem = forPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Item;

            // чек посоха и шифта
            bool holdsWand = activeItem is ItemWandOfTheForest;
            bool isSneaking = forPlayer.Entity.Controls.Sneak;

            if (holdsWand && isSneaking)
            {
                string linkStatus = TargetPos != null ? "Привязан" : "Не привязан";

                dsc.AppendLine($"{CurrentMana} / {MaxMana} [{linkStatus}]");
            }
        }
    }
}