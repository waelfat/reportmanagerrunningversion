// create a right panel to show current active executions for the user
//get panel element by id 
window.ActiveExecutionPanel={
   // add row to the panel
   addRow:function(execution){
       const $tbody = $('.executions-table tbody');
        $tbody.find('tr:has(td[colspan="4"])').remove();
        const $row = $('<tr>').attr('id', 'execution-' + execution.ExecutionID);
    },
   }
