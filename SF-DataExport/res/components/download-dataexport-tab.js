Vue.component('download-dataexport-tab', {
    template,
    data() { return { fetchExportPath: '' }; },
    watch: {
        fetchExportPath(search) {
            if (search && search !== this.search) {
                this.$store.state.dispatch$.next({
                    type: 'fetchDirPath', payload: {
                        search, value: this.$store.state.exportPath, field: 'exportPathItems'
                    }
                });
            }
        },
    },
    computed: {
        cmdExport() {
            const orgName = this.$store.state.currentInstanceUrl ? this.$store.state.currentInstanceUrl
                .replace(/^https:\/\/|\.my\.salesforce\.com$|\.salesforce\.com$/ig, '')
                .replace(' ', '-')
                .toLowerCase() : '';
            const exportEmails = this.exportEmails ? " -email \"" + this.exportEmails + "\"" : "";
            const exportPath = this.exportPath ? " -path \"" + this.exportPath + "\"" : "";
            return this.$store.state.cmdExport + orgName + exportEmails + exportPath;
        },
        currentInstanceUrl() { return this.$store.state.currentInstanceUrl; },
        exportEmails: {
            get() { return this.$store.state.exportEmails; },
            set(value) { this.dispatch('exportEmails', value); },
        },
        exportPath: {
            get() { return this.$store.state.exportPath; },
            set(value) { this.dispatch('exportPath', value); },
        },
        exportPathItems() { return this.$store.state.exportPathItems; },
        exportPercent() {
            const { exportCount, exportResultFiles } = this.$store.state;
            if (!exportCount) return 0;
            let completed = 0;
            for (const file in exportResultFiles) if (/^(?:Downloaded|Failed|Skipped)\.\.\./.test(exportResultFiles[file])) completed++;
            return completed / exportCount * 100;
        },
        exportResult() {
            return this.$store.state.exportResult;
        },
        exportDownloaded() {
            const { exportResultFiles } = this.$store.state;
            let downloaded = 0;
            for (const file in exportResultFiles) if (/^Downloaded\.\.\./.test(exportResultFiles[file])) downloaded++;
            return downloaded;
        },
        exportFailed() {
            const { exportResultFiles } = this.$store.state;
            let failed = 0;
            for (const file in exportResultFiles) if (/^Failed\.\.\./.test(exportResultFiles[file])) failed++;
            return failed;
        },
        exportSkipped() {
            const { exportResultFiles } = this.$store.state;
            let skipped = 0;
            for (const file in exportResultFiles) if (/^Skipped\.\.\./.test(exportResultFiles[file])) skipped++;
            return skipped;
        },
        exportResultFiles() {
            const { exportResultFiles } = this.$store.state;
            const files = exportResultFiles ? Object.keys(exportResultFiles) : [];
            return files.map(name => ({ name, result: exportResultFiles[name] }));
        },
        userAs() {
            const { userIdAs, users } = this.$store.state;
            for (let len = users.length, i = 0; i < len; i++) {
                const user = users[i];
                if (user.Id === userIdAs) return user;
            }
            return {};
        },
        userDisplayName() {
            return this.userAs.Name || '';
        },
        userEmail() {
            return this.userAs.Email || '';
        },
        userIdAs() { return this.$store.state.userIdAs; },
        userItems() {
            return this.$store.state.users.map(o => ({ text: o.Name + ' ' + o.Email, value: o.Id }));
        },
    },
});