window.ExecutionManager=  {
    // اسم المفتاح المستخدم في localStorage
    STORAGE_KEY: 'executions',

    // 1. جلب جميع العمليات
    getAll: function () {
        return JSON.parse(localStorage.getItem(this.STORAGE_KEY) || '{}');
    },

    // 2. إضافة عملية جديدة أو تحديثها بالكامل
    // المتوقع إرسال كائن يحتوي على: ExecutionID, UserName, ReportJob, Status, Started, Duration
    save: function (execution) {
        if (!execution || !execution.ExecutionID) {
            console.error("ExecutionID is required.");
            return false;
        }

        const executions = this.getAll();
        
        // حفظ أو استبدال البيانات بناءً على الـ ID
        executions[execution.ExecutionID] = {
            ExecutionID: execution.ExecutionID,
            UserName: execution.UserName || '',
            ReportJob: execution.ReportJob || '',
            Status: execution.Status || 'Pending',
            Started: execution.Started || new Date().toISOString(),
            Duration: execution.Duration || '00:00:00'
        };

        localStorage.setItem(this.STORAGE_KEY, JSON.stringify(executions));
        return true;
    },

    updateStatus: function (executionId, status, duration = null) {
        const executions = this.getAll();

        if (executions[executionId]) {
            executions[executionId].Status = status;
            if (duration !== null) {
                executions[executionId].Duration = duration;
            }
            localStorage.setItem(this.STORAGE_KEY, JSON.stringify(executions));
            return true;
        }
        
        console.warn(`Execution with ID ${executionId} not found.`);
        return false;
    },

    // 4. جلب عملية واحدة محددة
    getById: function (executionId) {
        const executions = this.getAll();
        return executions[executionId] || null;
    },

    // 5. حذف عملية معينة
    delete: function (executionId) {
        const executions = this.getAll();
        if (executions[executionId]) {
            delete executions[executionId];
            localStorage.setItem(this.STORAGE_KEY, JSON.stringify(executions));
            return true;
        }
        return false;
    }
};
