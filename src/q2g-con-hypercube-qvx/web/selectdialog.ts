/* License
Copyright (c) 2017 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

import * as qvangular from "qvangular";
export =
    ["serverside", "standardSelectDialogService",
        function (serverside, standardSelectDialogService) {
            var dialogContentProvider = {
                getConnectionInfo: function () {
                    return qvangular.promise({
                        dbusage: false,
                        ownerusage: false,
                        dbseparator: ".",
                        ownerseparator: ".",
                        specialchars: '! "$&\"()*+,-/:;<>`{}~[]',
                        quotesuffix: '"',
                        quoteprefix: '"',
                        dbfirst: true,
                        keywords: []
                    });
                },
                getDatabases: function () {
                    return serverside.sendJsonRequest("getDatabases").then(function (response) {
                        return response.qDatabases;
                    });
                },
                getOwners: function (qDatabaseName) {
                    return qvangular.promise();
                },
                getTables: function (qDatabaseName, qOwnerName) {
                    return serverside.sendJsonRequest("getTables", qDatabaseName, qOwnerName).then(function (response) {
                        return response.qTables;
                    });
                },
                getFields: function (qDatabaseName, qOwnerName, qTableName) {
                    return serverside.sendJsonRequest("getFields", qDatabaseName, qTableName, qOwnerName).then(function (response) {
                        return response.qFields;
                    });
                },
                getPreview: function (qDatabaseName, qOwnerName, qTableName) {
                    return serverside.sendJsonRequest("getPreview", qDatabaseName, qTableName, qOwnerName).then(function (response) {
                        return qvangular.promise(response.qPreview);
                    });
                }
            };

            standardSelectDialogService.showStandardDialog(dialogContentProvider, {
                precedingLoadVisible: false,
                fieldsAreSelectable: true,
                allowFieldRename: false,               
                scriptGenerator: {
                    generateScriptForSelections (a, b, c, d) {
                        
                        var selections = a.computeFlatListOfSelectedDatabaseOwnerCombinations();
                        var script = "";
                        for (var i = 0; i < selections.length; i++) {
                            for (var j = 0; j < selections[i].owner.tables.length; j++) {
                                var table = selections[i].owner.tables[j];
                                if (table.nbrOfCheckedFields > 0) {
                                    script += "SQL SELECT ";
                                    for (var k = 0; k < table.fields.length; k++) {
                                        var field = table.fields[k];
                                        if (field.checked)
                                            script += "\"" + field.name + "\"\n,";
                                    }
                                    script = script.replace(/\n,\s*$/,     "");
                                    var myRegexp = /\[(.*)\]$/g;
                                    var match = myRegexp.exec(table.name);
                                    if (match != null && match.length > 0)
                                        var tableId = match[1];

                                    script += "\nFROM [" + selections[i].database.name + "].[" + tableId + "];\n";
                                }
                            }
                        }

                        return script;
                    },

                    maybeQuote(a, d, e) {
                        console.log(this, a, d, e);
                        return false;
                    }
                }
            });
        }];