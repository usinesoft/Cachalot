namespace Client.Console
{
    public class CommandConnect : CommandBase
    {
        public override bool TryExecute()
        {
            if (!CanExecute)
                return false;


            var server = "localhost";

            if (Params.Count > 0 && !Params[0].EndsWith(".xml")) server = Params[0];

            var port = 4848;

            if (Params.Count > 1) port = int.Parse(Params[1]);


            return true;
        }
    }
}