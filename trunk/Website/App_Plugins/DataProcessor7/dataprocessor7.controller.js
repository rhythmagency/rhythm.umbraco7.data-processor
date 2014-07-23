(function () {
	function mainFunction ($scope, $routeParams, $http) {
		
		$scope.properties = [];
		$scope.dataProcessors = [];
		$scope.resultMessage = null;
		$scope.disableProcessButton = true;
		
		$scope.processData = function () {
			$scope.resultMessage = null;
			if ($scope.properties.length === 0) {
				alert("Select a Data Processor");
				return;
			}
			$scope.disableProcessButton = true;
			var values = [];
			for (var i = 0; i < $scope.properties.length; i++) {
				values.push(encodeURIComponent($scope.properties[i].value));
			}
			var processor = encodeURIComponent($scope.selectedProcessor.label);
			var query = "?processor=" + processor + "&data=" + values.join("&data=");
			var requestUrl = "/Umbraco/Rhythm/RhythmDataProcessor/ProcessInputs" + query;
			$http.get(requestUrl).success(function (data) {
				$scope.resultMessage = data.Message;
				$scope.disableProcessButton = false;
			});
		}
		
		$scope.showProcessor = function () {
			$scope.resultMessage = null;
			var selected = $scope.selectedProcessor;
			if (!selected || !selected.label) {
				$scope.properties = [];
				$scope.disableProcessButton = true;
				return;
			}
			$scope.disableProcessButton = false;
			var values = [];
			var processor = encodeURIComponent($scope.selectedProcessor.label);
			var query = "?processor=" + processor;
			var requestUrl = "/Umbraco/Rhythm/RhythmDataProcessor/GetProcessorInputs" + query;
			$http.get(requestUrl).success(function (data) {
				var newProps = [];
				for (var i = 0; i < data.length; i++) {
					var kind = data[i].Kind;
					var label = data[i].Label;
					if (kind === "Text") {
						newProps.push({
							label: label,
							view: "textbox",
							editor: "Umbraco.Textbox",
							value: "",
							config: {
							}
						});
					} else if (kind === "Nodes" || kind === "Node") {
						newProps.push({
							label: label,
							editor: "Umbraco.MultiNodeTreePicker",
							view: "contentpicker",
							config: {
								filter: null,
								maxNumber: null,
								minNumber: null,
								multiPicker: "1",
								startNode: {
									type: "content"
								}
							},
							value: ""
						});
					}
				}
				$scope.properties = newProps;
			});
		}
		
		$http.get("/Umbraco/Rhythm/RhythmDataProcessor/GetProcessors").success(function (data) {
			var items = [];
			for (var i = 0; i < data.length; i++) {
				items.push({label: data[i]});
			}
			$scope.dataProcessors = items;
		});
		
	}
	angular.module("umbraco").controller("RhythmDataProcessor", mainFunction);
})();