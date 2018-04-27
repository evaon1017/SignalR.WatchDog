<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="DashboardWeb.Default" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
<meta http-equiv="Content-Type" content="text/html; charset=utf-8"/>
    <title></title>
</head>
<body>
    <form id="form1" runat="server">
        
        <div id="hitCountValue">=</div>

        <script src="scripts/jquery-1.6.4.js"></script>
        <script src="scripts/jquery.signalR-2.2.3.js"></script>
        <script type="text/javascript">
            $(function () {
                var connection = $.hubConnection();
                var hub = connection.createHubProxy("hitCounter");
                hub.on("onRecordHit", function (hitCount) {
                    $('#hitCountValue').text(hitCount);
                });

                connection.start(function() {
                    hub.invoke('recordHit');
                });
            })
        </script>
    </form>
</body>
</html>
