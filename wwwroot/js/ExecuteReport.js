function showExecToast(message, type) {
    const toast = document.getElementById('execToast');
    if (!toast) return;
    toast.className = `toast align-items-center text-white border-0 bg-${type === 'success' ? 'success' : 'danger'}`;
    document.getElementById('execToastMessage').textContent = message;
    new bootstrap.Toast(toast, { delay: 3000 }).show();
}

function showDeleteConfirm(onConfirm) {
    const modal = new bootstrap.Modal(document.getElementById('deleteConfirmModal'));
    const btn = document.getElementById('deleteConfirmBtn');
    const handler = function () {
        btn.removeEventListener('click', handler);
        modal.hide();
        onConfirm();
    };
    btn.addEventListener('click', handler);
    modal.show();
}

$(function () {
    // Delegated handler for dynamically generated sidebar links
    $(document).on('click', '.load-report', function (e) {
        e.preventDefault();
        var $link = $(this);

        var id = $link.data('report-id');
        if (!id) return;

        // UI feedback: set active state and show spinner in container
        $('.load-report.active').removeClass('active');
        $link.addClass('active');
        $('#ReportContainer').html('<div class="text-center py-4"><div class="spinner-border" role="status" aria-hidden="true"></div></div>');

        $.ajax({
            url: '/Home/ExecuteReport',
            type: 'GET',
            data: { id: id },
            success: function (result) {
                $('#ReportContainer').html(result);
                $('#ReportContainer [data-bs-toggle="tooltip"]').each(function () {
                    bootstrap.Tooltip.getOrCreateInstance(this);
                });
                if (typeof IntializePartial === 'function') {
                    IntializePartial(id);
                } else {
                    $.getScript('/js/executereportpartial.js')
                        .done(function () {
                            if (typeof IntializePartial === 'function') IntializePartial(id);
                        })
                        .fail(function () {
                            console.error('Failed to load executerepotpartial.js');
                        });
                }
                initializeCascadingSelects();
            },
            error: function (xhr) {
                if(xhr.status === 401) {
                    // If the user is not authenticated, redirect to the login page
                    window.location.href = '/Auth/Login';
                } else {
                    $('#ReportContainer').html('<div class="alert alert-danger">Failed to load report.</div>');
                }
              
            }
        });
    });
    
    // Initialize cascading selects
    function initializeCascadingSelects() {
        console.log('Initializing cascading selects');
        $('.cascading-select').each(function() {
            const $select = $(this);
            const allOptionsJson = $select.data('all-options');
            const dependsOn = $select.data('depends-on');
            
            console.log('Select:', $select.data('param-name'), 'DependsOn:', dependsOn, 'HasData:', !!allOptionsJson);
            
            if (!allOptionsJson) return;
            
            try {
                // debugger;
                const allOptions = allOptionsJson;
                console.log('Parsed options for', $select.data('param-name'), ':', allOptions);
                
                if (!dependsOn) {
                    // Root select - populate with all data
                    populateSelect($select, allOptions);
                } else {
                    // Dependent select - start empty
                    const $parentSelect = $(`.cascading-select[data-param-name="${dependsOn}"]`);
                    populateSelect($select, allOptions);
                    
                    $parentSelect.on('change', function() {
                        const parentValue = $(this).val();
                        
                        if (parentValue) {
                            const filteredOptions = allOptions.filter(opt => 
                                opt.parentValue == parentValue
                            );
                            populateSelect($select, filteredOptions);
                        } else {
                            // $select.html('<option value="">Select an option...</option>');
                            populateSelect($select,allOptions)
                        }
                    });
                }
            } catch(e) {
                console.error('Error parsing options:', e);
            }
        });
    }
    
    function populateSelect($select, data) {
        $select.html('<option value="">Select an option...</option>');
        //sord data by text
       // data.sort((a, b) => a.text.localeCompare(b.text));
        data.forEach(function(item) {
            $select.append(`<option value="${item.value}">${item.text}</option>`);
        });
    }
    
    // Schedule datetime change handler
    $(document).on('change', '[id^="scheduleDateTime-"]', function() {
        const reportId = $(this).attr('id').replace('scheduleDateTime-', '');
        const dateTime = new Date($(this).val());
        
        if (!isNaN(dateTime.getTime())) {
            const minute = dateTime.getMinutes();
            const hour = dateTime.getHours();
            const day = dateTime.getDate();
            const month = dateTime.getMonth() + 1;
            const cronExpr = `${minute} ${hour} ${day} ${month} *`;
            
            $(`#cronExpression-${reportId}`).val(cronExpr);
            
            try {
                const humanReadable = typeof cronstrue !== 'undefined' && typeof cronstrue.toString === 'function'
                    ? cronstrue.toString(cronExpr)
                    : 'Cron description unavailable';
                $(`#cronDescription-${reportId}`).text(humanReadable);
            } catch (e) {
                $(`#cronDescription-${reportId}`).text('Invalid cron expression');
            }
        }
    });
     // add on click to fa-star to be send to server with reportid and convert it to favorite filled in with yellow
  $(document).on('click', '[id="favorite-btn"]', function () {
    var reportId = $(this).data("report-id");
    var $icon = $(this).find("i");
    var isFavorite = $icon.hasClass("fa-solid");
    console.log('isFavorite = ' + isFavorite);
    

    $.ajax({
      url: "/Home/ToggleFavorite",
      method: "POST",
      data: { reportId: reportId },
      success: function (resp) {
        if (resp.isFavorite) {
          $icon.removeClass("fa-regular").addClass("fa-solid").css("color", "gold");
          //change button text to unfavorite
          $("favorite-btn").text('Unfavorite');
          showExecToast('Report added to favorites', 'success');
          // Add report to the favorites list in the sidebar
            var $favoritesList = $("#favoritesList");
            if ($favoritesList.length) {
                var $newFavorite = $(`<li class="nav-item"><a class="nav-link load-report" href="#" data-report-id="${reportId}">${resp.reportName}</a></li>`);
                $favoritesList.append($newFavorite);
            }
        } else {
          $icon.removeClass("fa-solid").addClass("fa-regular").css("color", "");
            //change button text to favorite
            $("favorite-btn").text('Favorite');
            // remove report from the favorites list in the sidebar
            var $favoritesList = $("#favoritesList");
            if ($favoritesList.length) {
                $favoritesList.find(`a[data-report-id="${reportId}"]`).closest("li").remove();
            }
          showExecToast('Report removed from favorites', 'success');
        }
      },
      error: function () {
        showExecToast('Failed to toggle favorite', 'error');
      },
    });
  });
    
    // Schedule button handler
    $(document).on('click', '[id^="scheduleBtn-"]', function() {
        const reportId = $(this).attr('id').replace('scheduleBtn-', '');
        const cronExpression = $(`#cronExpression-${reportId}`).val();
        const selectedDate = $(`#scheduleDateTime-${reportId}`).val();
        console.log(selectedDate);
        //check if user enter date and time in scheduleDateTime-
        if(!selectedDate)
        {
            showExecToast('Please select a date and time', 'error');
            return;
        }
        //ensure selectedDate is in the future and at least 2 minutes from now
        const selectedDateTime = new Date(selectedDate);
        const now = new Date();
        if(selectedDateTime <= now)
        {
            showExecToast('Please select a future date and time', 'error');
            return;
        }
        if(selectedDateTime <= new Date(now.getTime() + 2 * 60000))
        {
            showExecToast('Please select a date and time at least 2 minutes from now', 'error');
            return;
        }
        //ensure paramereters are valid before sending to server
         let valid = true;
        $(`#executeReportForm-${reportId} input[name^="Parameters"]`).each(function() {
            const name = $(this).attr('name');
            if (name.includes('.Name')) {
                const index = name.match(/\[(\d+)\]/)[1];
                const paramName = $(this).val();
                const paramValue = $(`input[name="Parameters[${index}].Value"], select[name="Parameters[${index}].Value"]`).val();
                if (!paramName) {
                    //alert('Parameter name cannot be empty');
                    //show error message under the input
                    $(this).addClass('is-invalid');
                    $(this).after('<div class="invalid-feedback">Parameter name cannot be empty</div>');
                    
                    valid = false;
                    return false; // break loop
                }
                if (!paramValue) {
                    //alert(`Please provide a value for parameter: ${paramName}`);
                    //show error message under the input
                    $(`input[name="Parameters[${index}].Value"], select[name="Parameters[${index}].Value"]`).addClass('is-invalid');
                    $(`input[name="Parameters[${index}].Value"], select[name="Parameters[${index}].Value"]`).after('<div class="invalid-feedback">Parameter value cannot be empty</div>');

                    valid = false;
                    return false; // break loop
                }
            }
        });
        if(!valid) return;


        const $btn = $(this);
        
        if (!cronExpression) {
            showExecToast('Please select a date and time first', 'error');
            return;
        }
        
        // Collect parameters from the form
        const parameters = [];
        $(`#executeReportForm-${reportId} input[name^="Parameters"]`).each(function() {
            const name = $(this).attr('name');
            if (name.includes('.Name')) {
                const index = name.match(/\[(\d+)\]/)[1];
                const paramName = $(this).val();
                const paramValue = $(`input[name="Parameters[${index}].Value"], select[name="Parameters[${index}].Value"]`).val();
                parameters.push({ Name: paramName, Value: paramValue || '' });
            }
        });
        //ensure that all parameters are filled before sending to server
        // for(let i=0;i<parameters.length;i++)
        // {
        //     if(!parameters[i].Name || !parameters[i].Value)
        //     {
        //         // show error message under the input 
        //         $(`#executeReportForm-${reportId} input[name="Parameters[${i}].Name"]`).addClass('is-invalid');
        //         $(`#executeReportForm-${reportId} input[name="Parameters[${i}].Name"]`).after('<div class="invalid-feedback">Parameter name cannot be empty</div>');
        //         $(`input[name="Parameters[${i}].Value"], select[name="Parameters[${i}].Value"]`).addClass('is-invalid');
        //         $(`input[name="Parameters[${i}].Value"], select[name="Parameters[${i}].Value"]`).after('<div class="invalid-feedback">Parameter value cannot be empty</div>');
        //         return;
        //     }
        // }





        
        $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-2"></i>Scheduling...');
        
        $.ajax({
            url: '/Home/ScheduleReport',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                reportId: reportId,
                cronExpression: cronExpression,
                selectedDate:selectedDate,
                parameters: parameters
            }),
            success: function(data) {
                showExecToast('Report scheduled successfully!', 'success');
                $(`#scheduleSection-${reportId}`).collapse('hide');
                $btn.prop('disabled', false).html('<i class="fas fa-calendar-check me-2"></i>Schedule Report');
                // debugger;
                addExecutionToTable(data.executionId,true,data.scheduledTime,data.parameters);
                //remove all error messages and invalid classes
                $(`#executeReportForm-${reportId} .is-invalid`).removeClass('is-invalid');
                $(`#executeReportForm-${reportId} .invalid-feedback`).remove();
                
            },
            error: function(xhr) {
                const errorMsg = xhr.responseJSON?.error || 'Failed to schedule report';
                showExecToast(errorMsg, 'error');
                $btn.prop('disabled', false).html('<i class="fas fa-calendar-check me-2"></i>Schedule Report');
            }
        });
    });
    
    // Delete execution handler
    $(document).on('click', '.delete-execution', function() {
        var executionId = $(this).data('execution-id');
        var $btn = $(this);

        showDeleteConfirm(function() {
            $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i>Deleting...');

            $.ajax({
                url: '/Home/DeleteExecution',
                method: 'POST',
                data: { executionId: executionId },
                success: function() {
                    $(`tr[data-execution-id="${executionId}"]`).fadeOut(function() {
                        $(this).remove();
                    });
                },
                error: function() {
                    showExecToast('Failed to delete execution', 'error');
                    $btn.prop('disabled', false).html('<i class="fas fa-trash me-1"></i>Delete');
                }
            });
        });
    });
});
