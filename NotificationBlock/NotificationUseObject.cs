using Sandbox.ModAPI;
using VRage.Game.Entity.UseObject;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace ZoneControl.NotificationBlock
{
    [MyUseObject("zonenote")]
    public class Button1 : MyUseObjectBase
    {
        private IMyTextPanel block;
        private NotificationComponent component;

        public override UseActionEnum SupportedActions => UseActionEnum.Manipulate;
        // | UseActionEnum.Close
        // | UseActionEnum.BuildPlanner
        // | UseActionEnum.OpenInventory
        // | UseActionEnum.OpenTerminal
        // | UseActionEnum.PickUp
        // | UseActionEnum.UseFinished; // gets called when releasing manipulate

        // What action gets sent to Use() when interacted with PrimaryAttack or Use binds.
        public override UseActionEnum PrimaryAction => UseActionEnum.Manipulate;

        // What action gets sent to Use() when interacted with SecondaryAttack or Inventory/Terminal binds.
        public override UseActionEnum SecondaryAction => UseActionEnum.OpenTerminal;


        public Button1(IMyEntity owner, string dummyName, IMyModelDummy dummyData, uint shapeKey) : base(owner, dummyData)
        {
            block = owner as IMyTextPanel;
            component = block.GameLogic.GetAs<NotificationComponent>();
        }

        public override MyActionDescription GetActionInfo(UseActionEnum actionEnum)
        {
            switch (actionEnum)
            {
                default:
                    return default(MyActionDescription);

                case UseActionEnum.Manipulate:
                    return new MyActionDescription()
                    {
                        Text = MyStringId.GetOrCompute("Press to select"),
                        IsTextControlHint = true,
                    };
            }
        }

        public override void Use(UseActionEnum actionEnum, IMyEntity user)
        {
            switch (actionEnum)
            {
                case UseActionEnum.Manipulate:
                    {
                        var button = int.Parse(this.Dummy.Name.Substring(this.Dummy.Name.LastIndexOf('_') + 1));
                        if (component != null)
                        {
                            component.ButtonPressed(user, button);
                        }

                        break;
                    }
            }
        }

    }
}